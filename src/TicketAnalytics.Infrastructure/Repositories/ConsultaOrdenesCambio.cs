using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Common.Models;
using TicketAnalytics.Application.Tickets.DTOs;
using TicketAnalytics.Domain.Common;
using TicketAnalytics.Domain.Enums;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;
using TicketAnalytics.Infrastructure.Persistence.Sdm.Entities;
using TicketAnalytics.Infrastructure.Services.Parametros;

namespace TicketAnalytics.Infrastructure.Repositories;

/// <summary>Fila mínima de una OC con su vencimiento ya calculado EN SQL.</summary>
/// <summary>Complejidad de una OC (base local) enviada a MDB como JSON: chg_ref_num → nivel.</summary>
public sealed class RefNivel
{
    public string Ref { get; set; } = "";
    public int Nivel { get; set; }
}

public sealed class FilaOc
{
    public int Id { get; set; }
    public string Ref { get; set; } = "";
    public byte[]? Assignee { get; set; }
    public string? Status { get; set; }
    public string? Category { get; set; }
    public int? OpenDate { get; set; }
    public int? CloseDate { get; set; }
    /// <summary>Epoch del vencimiento = open_date + días(complejidad) * 86400. Null si no hay apertura.</summary>
    public int? Venc { get; set; }
    /// <summary>3 Alta, 2 Media, 1 Baja, 0 SinAsignar (registrado), -1 sin registro de complejidad.</summary>
    public int Nivel { get; set; }
}

/// <summary>
/// Motor de consultas de Órdenes de Cambio sobre MDB.
///
/// PROBLEMA que resuelve: antes se traían a memoria TODAS las OC de la ventana con sus tickets,
/// contactos y catálogos, y luego se filtraba/agrupaba en C#. Ahora:
///  - El vencimiento se calcula en SQL: open_date + días(complejidad) * 86400. La complejidad vive en
///    la base local, así que viaja como 4 listas de chg_ref_num (Alta/Media/Baja/SinAsignar), cada una
///    UN solo parámetro JSON (OPENJSON): sin JOIN entre bases y sin el límite de 2.100 parámetros.
///  - Todos los filtros (vencidas, vencen en N días, mes de vencimiento, analista, texto…) son
///    predicados SQL sobre columnas sin funciones, para que entren por índice.
///  - Los conteos y distribuciones son GROUP BY en SQL; a memoria solo llegan filas agregadas o la
///    página que se muestra.
///
/// ÍNDICES esperados (producción): chg_x6 (category, active_flag) o chg_x0 (open_date, chg_ref_num)
/// para la ventana; call_req_x17 (change) para los EXISTS de tickets relacionados.
/// </summary>
public sealed class ConsultaOrdenesCambio
{
    private readonly SdmReadContext _sdm;
    private readonly AnalyticsContext _analytics;
    private readonly IParametrosProvider _parametros;
    private readonly CatalogoSdm _catalogo;

    public ConsultaOrdenesCambio(SdmReadContext sdm, AnalyticsContext analytics, IParametrosProvider parametros, CatalogoSdm catalogo)
    {
        _sdm = sdm;
        _analytics = analytics;
        _parametros = parametros;
        _catalogo = catalogo;
    }

    /// <summary>Límites de tiempo del "hoy" expresados en epoch, calculados una vez por request.</summary>
    public sealed class Reloj
    {
        public DateTime Hoy { get; init; }
        public ParametrosNegocio P { get; init; } = new();

        /// <summary>
        /// Menor epoch e tal que EpochRD.ToDateTime(e) &gt;= fecha. Con esto, "fechaDeLaColumna &lt; fecha"
        /// equivale exactamente a "columna &lt; Limite(fecha)" (la base de EpochRD tiene milisegundos).
        /// </summary>
        public static int Limite(DateTime fecha) => (int)Math.Ceiling((fecha - EpochRD.BaseTicket).TotalSeconds);

        /// <summary>Segundos (con decimales) entre la base de EpochRD y la fecha: para FLOOR/CEILING en SQL.</summary>
        public static double Offset(DateTime fecha) => (fecha - EpochRD.BaseTicket).TotalSeconds;

        public int HoyE => Limite(Hoy);
        public int DiaE(int dias) => Limite(Hoy.AddDays(dias));
        public double HoyOffset => Offset(Hoy);
        public int AhoraE => Limite(DateTime.Now);
    }

    public async Task<Reloj> RelojAsync(CancellationToken ct)
        => new() { Hoy = DateTime.Today, P = await _parametros.ObtenerAsync(ct) };

    /// <summary>Códigos de chgcat del área (catálogo en caché), opcionalmente filtrados por nombre exacto.</summary>
    public async Task<List<string>> CategoriasAsync(List<string>? filtro, CancellationToken ct)
    {
        var cats = await _catalogo.CategoriasOcAreaAsync(ct);
        return cats.Where(c => filtro is not { Count: > 0 }
                               || filtro.Any(f => string.Equals((c.Value ?? "").Trim(), f.Trim(), StringComparison.OrdinalIgnoreCase)))
                   .Select(c => c.Key).Distinct().ToList();
    }

    /// <summary>
    /// Ventana de apertura de las OC a considerar. Igual que antes: desde (AperturaDesde ?? FechaBase)
    /// menos la "ventana" (mayor plazo por complejidad + margen; 90 + 10 = 100 días con los valores
    /// actuales), para no perder OC abiertas antes que vencen dentro del período.
    /// </summary>
    public static (int Desde, int? Hasta) Ventana(Reloj r, DateTime? aperturaDesde, DateTime? aperturaHasta)
    {
        var desde = EpochRD.ToEpoch((aperturaDesde ?? r.P.FechaBase).AddDays(-r.P.OcDiasVentana));
        int? hasta = aperturaHasta.HasValue ? EpochRD.FinDelDiaExclusivo(aperturaHasta.Value) : null;
        return (desde, hasta);
    }

    /// <summary>
    /// Consulta base de OC con el vencimiento calculado. Solo aplica la ventana y la categoría del área.
    /// Devuelve un IQueryable: todavía no se ejecuta nada en el servidor.
    /// </summary>
    public async Task<IQueryable<FilaOc>> BaseAsync(Reloj r, List<string> categorias, int desde, int? hasta,
                                                    Func<IQueryable<Chg>, IQueryable<Chg>>? filtroChg, CancellationToken ct)
    {
        // Complejidades (base local) → UN parámetro JSON [{"r":ref,"n":nivel}], que SQL Server convierte
        // en tabla una sola vez y une por LEFT JOIN. Antes eran cuatro listas con "IN (OPENJSON...)"
        // que se evaluaban por fila (y otra vez por cada bucket del GROUP BY): consultas de ~400
        // lecturas gastaban 30-50 ms de CPU.
        var comp = await _analytics.OrdenCambioComplejidades.AsNoTracking()
            .Select(o => new { o.ChgRefNum, o.Complejidad }).ToListAsync(ct);
        var compJson = JsonSerializer.Serialize(comp.Select(c => new { r = c.ChgRefNum, n = NivelDe(c.Complejidad) }));
        var niveles = _sdm.Database.SqlQuery<RefNivel>(
            $"SELECT j.r AS Ref, j.n AS Nivel FROM OPENJSON({compJson}) WITH (r NVARCHAR(60) '$.r', n INT '$.n') AS j");
        int sA = r.P.OcDiasAlta * 86400, sM = r.P.OcDiasMedia * 86400, sB = r.P.OcDiasBaja * 86400, sS = r.P.OcDiasSinAsignar * 86400;

        IQueryable<Chg> q = _sdm.Changes.AsNoTracking()
            .Where(c => c.category != null && categorias.Contains(c.category)
                        && c.open_date >= desde && (hasta == null || c.open_date < hasta));
        if (filtroChg is not null) q = filtroChg(q);

        return from c in q
               join k in niveles on c.chg_ref_num equals k.Ref into gk
               from k in gk.DefaultIfEmpty()
               // RefNivel no tiene clave y EF no traduce compararlo con null: se usa la columna, que es NULL sin coincidencia.
               let n = (int?)k.Nivel
               select new FilaOc
               {
                   Id = c.id,
                   Ref = c.chg_ref_num,
                   Assignee = c.assignee,
                   Status = c.status,
                   Category = c.category,
                   OpenDate = c.open_date,
                   CloseDate = c.close_date,
                   Nivel = n ?? -1,
                   // Sin complejidad registrada (o "SinAsignar") se usa el plazo de SinAsignar, como antes.
                   Venc = c.open_date == null ? null
                        : c.open_date + (n == 3 ? sA : n == 2 ? sM : n == 1 ? sB : sS)
               };
    }

    /// <summary>Nivel numérico usado en SQL (3 Alta, 2 Media, 1 Baja, 0 SinAsignar); coincide con EtiquetaNivel.</summary>
    private static int NivelDe(NivelComplejidad n) => n switch
    {
        NivelComplejidad.Alta => 3,
        NivelComplejidad.Media => 2,
        NivelComplejidad.Baja => 1,
        _ => 0
    };

    /// <summary>Aplica en SQL los filtros del módulo de OC (los mismos que antes se aplicaban en memoria).</summary>
    public async Task<IQueryable<FilaOc>> FiltradaAsync(OrdenCambioFiltro f, Reloj r, CancellationToken ct, bool aplicarFiltros = true)
    {
        var cats = await CategoriasAsync(f.Categorias, ct);
        if (cats.Count == 0) return Enumerable.Empty<FilaOc>().AsQueryable();
        var (desde, hasta) = Ventana(r, f.AperturaDesde, f.AperturaHasta);

        // Filtros que se resuelven sobre la tabla chg antes de proyectar.
        var estadosCodigos = await CodigosEstadoAsync(f.Estados, ct);
        var uuidsAnalista = string.IsNullOrWhiteSpace(f.AsignatarioNombre) || !aplicarFiltros
            ? null
            : await _catalogo.UuidsPorNombreAsync(f.AsignatarioNombre, contiene: true, ct);

        IQueryable<Chg> FiltroChg(IQueryable<Chg> q)
        {
            if (!aplicarFiltros) return q;
            if (!string.IsNullOrWhiteSpace(f.NoOC)) q = q.Where(c => c.chg_ref_num.Contains(f.NoOC));
            if (!string.IsNullOrWhiteSpace(f.Texto))
                q = q.Where(c => (c.summary ?? "").Contains(f.Texto) || (c.description ?? "").Contains(f.Texto) || c.chg_ref_num.Contains(f.Texto));
            if (estadosCodigos is not null) q = q.Where(c => c.status != null && estadosCodigos.Contains(c.status));
            if (uuidsAnalista is not null) q = ConsultasSdm.FiltrarPorUuid(q, c => c.assignee, uuidsAnalista);
            if (f.SinAnalista == true) q = q.Where(c => c.assignee == null);
            if (f.ConTicketsRelacionados == true) q = q.Where(c => _sdm.CallReqs.Any(cr => cr.change == c.id));
            if (f.ConTicketCerradoOcPendiente == true)
                q = q.Where(c => (c.close_date == null || c.close_date == 0)
                                 && _sdm.CallReqs.Any(cr => cr.change == c.id && cr.active_flag == 0));
            if (f.MinTickets.HasValue) q = q.Where(c => _sdm.CallReqs.Count(cr => cr.change == c.id) >= f.MinTickets.Value);
            if (f.MaxTickets.HasValue) q = q.Where(c => _sdm.CallReqs.Count(cr => cr.change == c.id) <= f.MaxTickets.Value);
            return q;
        }

        var q = await BaseAsync(r, cats, desde, hasta, FiltroChg, ct);
        if (!aplicarFiltros) return q;

        if (!string.IsNullOrWhiteSpace(f.ComplejidadOC))
        {
            var nivel = f.ComplejidadOC.Trim().ToLowerInvariant() switch
            {
                "alta" => 3, "media" => 2, "baja" => 1, "sinasignar" => 0, _ => int.MinValue
            };
            q = q.Where(o => o.Nivel == nivel);
        }

        if (f.CierreDesde.HasValue) { var e = Reloj.Limite(f.CierreDesde.Value); q = q.Where(o => o.CloseDate != 0 && o.CloseDate >= e); }
        if (f.CierreHasta.HasValue) { var e = Reloj.Limite(f.CierreHasta.Value.AddTicks(1)); q = q.Where(o => o.CloseDate != 0 && o.CloseDate < e); }
        if (f.VencimientoDesde.HasValue) { var e = Reloj.Limite(f.VencimientoDesde.Value); q = q.Where(o => o.Venc >= e); }
        if (f.VencimientoHasta.HasValue) { var e = Reloj.Limite(f.VencimientoHasta.Value.AddTicks(1)); q = q.Where(o => o.Venc < e); }

        // Mes / año de vencimiento -> rangos de epoch (sin funciones sobre la columna).
        if (f.MesVencimiento.HasValue || f.AnioVencimiento.HasValue)
        {
            var anios = f.AnioVencimiento.HasValue
                ? new[] { f.AnioVencimiento.Value }
                : Enumerable.Range(r.P.FechaBase.Year - 1, r.Hoy.Year - r.P.FechaBase.Year + 3).ToArray();
            var rangos = anios.SelectMany(a => f.MesVencimiento.HasValue
                    ? new[] { (Reloj.Limite(new DateTime(a, f.MesVencimiento.Value, 1)), Reloj.Limite(new DateTime(a, f.MesVencimiento.Value, 1).AddMonths(1))) }
                    : new[] { (Reloj.Limite(new DateTime(a, 1, 1)), Reloj.Limite(new DateTime(a + 1, 1, 1))) })
                .ToList();
            q = q.Where(ConsultasSdm.EnAlgunRango<FilaOc>(o => o.Venc, rangos));
        }

        int hoy = r.HoyE, manana = r.DiaE(1);
        if (f.Vencidas == true) q = q.Where(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc < hoy);
        if (f.VencenHoy == true) q = q.Where(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc >= hoy && o.Venc < manana);
        if (f.SinFechaVencimiento == true) q = q.Where(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc == null);
        if (f.VencenEsteMes == true)
        {
            int m0 = Reloj.Limite(new DateTime(r.Hoy.Year, r.Hoy.Month, 1)), m1 = Reloj.Limite(new DateTime(r.Hoy.Year, r.Hoy.Month, 1).AddMonths(1));
            q = q.Where(o => o.Venc >= m0 && o.Venc < m1);
        }
        if (f.VencenProximoMes == true)
        {
            var pm = new DateTime(r.Hoy.Year, r.Hoy.Month, 1).AddMonths(1);
            int m0 = Reloj.Limite(pm), m1 = Reloj.Limite(pm.AddMonths(1));
            q = q.Where(o => o.Venc >= m0 && o.Venc < m1);
        }
        if (f.VencenEnDias is { } dias) { int fin = r.DiaE(dias + 1); q = q.Where(o => o.Venc >= hoy && o.Venc < fin); }
        if (f.SoloAbiertas == true) q = q.Where(o => o.CloseDate == null || o.CloseDate == 0);
        if (f.SoloCerradas == true) q = q.Where(o => o.CloseDate != null && o.CloseDate != 0);
        return q;
    }

    /// <summary>Orden de los listados (el mismo de antes) con desempate estable por id.</summary>
    public static IQueryable<FilaOc> Ordenar(IQueryable<FilaOc> q, OrdenCambioFiltro f)
        => (f.OrderBy?.ToLowerInvariant(), f.OrderDesc) switch
        {
            ("apertura", true) => q.OrderByDescending(o => o.OpenDate).ThenBy(o => o.Id),
            ("apertura", false) => q.OrderBy(o => o.OpenDate).ThenBy(o => o.Id),
            ("noocv", _) or ("nooc", true) => q.OrderByDescending(o => o.Ref),
            ("nooc", false) => q.OrderBy(o => o.Ref),
            ("diasrestantes", true) => q.OrderByDescending(o => o.Venc ?? int.MaxValue).ThenBy(o => o.Id),
            ("diasrestantes", false) => q.OrderBy(o => o.Venc ?? int.MaxValue).ThenBy(o => o.Id),
            _ => q.OrderBy(o => o.Venc ?? int.MaxValue).ThenBy(o => o.Id)
        };

    /// <summary>Estados escritos como texto en la UI (descripción de chgstat) → códigos de MDB.</summary>
    private async Task<List<string>?> CodigosEstadoAsync(List<string>? estados, CancellationToken ct)
    {
        if (estados is not { Count: > 0 }) return null;
        var mapa = await _catalogo.EstadosOcAsync(ct);
        return mapa.Where(kv => estados.Contains(kv.Value) || estados.Contains(kv.Key)).Select(kv => kv.Key).ToList();
    }

    /// <summary>Texto legible de un estado de OC (como antes: descripción, sym o el código).</summary>
    public async Task<Func<string?, string>> TextoEstadoAsync(CancellationToken ct)
    {
        var mapa = await _catalogo.EstadosOcAsync(ct);
        return code => code is null ? "Sin estado" : mapa.TryGetValue(code, out var t) ? t : code;
    }

    /// <summary>Nombre "Nombre Apellido" de cada UUID; null si no está en ca_contact (igual que antes).</summary>
    public async Task<Func<byte[]?, string?>> NombresAsync(IEnumerable<byte[]?> uuids, CancellationToken ct)
    {
        var mapa = await _catalogo.ContactosAsync(uuids, ct);
        return u => u is null ? null : mapa.TryGetValue(Convert.ToHexString(u), out var n) ? n.Completo : null;
    }

    /// <summary>Nombre legible de la categoría de una OC (sym de chgcat o el código).</summary>
    public async Task<Func<string?, string?>> CategoriaLegibleAsync(CancellationToken ct)
    {
        var mapa = await _catalogo.CategoriasOcAreaAsync(ct);
        return code => code is null ? null : mapa.TryGetValue(code, out var s) ? (s ?? code) : code;
    }

    public static string EtiquetaNivel(int nivel) => nivel switch
    {
        3 => "Alta", 2 => "Media", 1 => "Baja", 0 => "SinAsignar", _ => "Sin asignar"
    };
}

/// <summary>Predicados reutilizables para MDB que EF no arma solo de forma eficiente.</summary>
public static class ConsultasSdm
{
    /// <summary>
    /// col = @u1 OR col = @u2 ... para pocas UUID (binary(16)). Usa el índice de la columna
    /// (p. ej. call_req_x4 / chg_x4 para assignee). Pensado para listas cortas (analistas).
    /// </summary>
    public static IQueryable<T> FiltrarPorUuid<T>(IQueryable<T> q, Expression<Func<T, byte[]?>> columna, IReadOnlyList<byte[]> uuids)
    {
        if (uuids.Count == 0) return q.Where(_ => false);
        var p = columna.Parameters[0];
        Expression? cuerpo = null;
        foreach (var u in uuids.Take(100))
        {
            // Cada UUID entra como parámetro (no como constante) para que el plan se reutilice.
            Expression<Func<byte[]>> valor = () => u;
            var igual = Expression.Equal(columna.Body, Expression.Convert(valor.Body, typeof(byte[])));
            cuerpo = cuerpo is null ? igual : Expression.OrElse(cuerpo, igual);
        }
        return q.Where(Expression.Lambda<Func<T, bool>>(cuerpo!, p));
    }

    /// <summary>(col &gt;= d1 AND col &lt; h1) OR (col &gt;= d2 AND col &lt; h2) ... sobre un epoch.</summary>
    public static Expression<Func<T, bool>> EnAlgunRango<T>(Expression<Func<T, int?>> columna, IReadOnlyList<(int Desde, int Hasta)> rangos)
    {
        var p = columna.Parameters[0];
        Expression cuerpo = Expression.Constant(false);
        foreach (var (d, h) in rangos)
        {
            Expression<Func<int?>> vd = () => d;
            Expression<Func<int?>> vh = () => h;
            var tramo = Expression.AndAlso(
                Expression.GreaterThanOrEqual(columna.Body, vd.Body),
                Expression.LessThan(columna.Body, vh.Body));
            cuerpo = cuerpo is ConstantExpression ? tramo : Expression.OrElse(cuerpo, tramo);
        }
        return Expression.Lambda<Func<T, bool>>(cuerpo, p);
    }
}
