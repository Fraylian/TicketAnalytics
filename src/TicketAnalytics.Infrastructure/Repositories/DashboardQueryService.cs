using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Dashboard.DTOs;
using TicketAnalytics.Application.Tickets.DTOs;
using TicketAnalytics.Domain.Common;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;
using TicketAnalytics.Infrastructure.Services.Parametros;

namespace TicketAnalytics.Infrastructure.Repositories;

/// <summary>
/// Indicadores del dashboard. Todas las consultas a MDB van acotadas por categoría del alcance
/// y fecha de apertura, y agrupan en SQL. Los nombres (categorías, estados, contactos) salen de
/// la caché de catálogos en vez de pedirse en cada request.
/// </summary>
public class DashboardQueryService : IDashboardQueryService
{
    private readonly SdmReadContext _sdm;
    private readonly IParametrosProvider _parametros;
    private readonly CatalogoSdm _catalogo;

    public DashboardQueryService(SdmReadContext sdm, IParametrosProvider parametros, CatalogoSdm catalogo)
    {
        _sdm = sdm;
        _parametros = parametros;
        _catalogo = catalogo;
    }

    private async Task<(List<string> alcance, int desde, int? hasta)> ResolverAsync(TicketFiltro f, CancellationToken ct)
    {
        var p = await _parametros.ObtenerAsync(ct);
        var alcance = (await _catalogo.AlcanceAsync(ct)).Select(a => a.Persid).ToList();
        if (f.Categorias is { Count: > 0 }) alcance = alcance.Where(x => f.Categorias.Contains(x)).ToList();
        int desde = f.FechaDesde.HasValue ? EpochRD.InicioDelDia(f.FechaDesde.Value) : EpochRD.InicioDelDia(p.FechaBase);
        int? hasta = f.FechaHasta.HasValue ? EpochRD.FinDelDiaExclusivo(f.FechaHasta.Value) : (int?)null;
        return (alcance, desde, hasta);
    }

    private async Task<IReadOnlyDictionary<string, string?>> CatMapAsync(CancellationToken ct) => await _catalogo.SymCategoriasTicketAsync(ct);

    public async Task<IReadOnlyList<VolumenPorEstadoDto>> VolumenPorEstadoAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var (alcance, desde, hasta) = await ResolverAsync(f, ct);
        var raw = await (from cr in _sdm.CallReqs.AsNoTracking()
                         where alcance.Contains(cr.category!) && cr.open_date >= desde && (hasta == null || cr.open_date < hasta)
                         group cr by new { cr.category, cr.status } into g
                         select new { g.Key.category, g.Key.status, Total = g.Count(), Abiertos = g.Count(x => x.active_flag == 1), Cerrados = g.Count(x => x.active_flag == 0) }).ToListAsync(ct);
        var catMap = await CatMapAsync(ct);
        var estMap = await _catalogo.EstadosTicketAsync(ct);
        return raw.Select(r => new VolumenPorEstadoDto
        {
            Categoria = r.category != null && catMap.TryGetValue(r.category, out var c) ? c ?? r.category : r.category ?? "",
            Estado = r.status != null && estMap.TryGetValue(r.status, out var e) ? e ?? r.status : r.status ?? "",
            Total = r.Total, Abiertos = r.Abiertos, Cerrados = r.Cerrados
        }).OrderBy(x => x.Categoria).ThenByDescending(x => x.Total).ToList();
    }

    public async Task<IReadOnlyList<CumplimientoSlaDto>> CumplimientoSlaAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var (alcance, desde, hasta) = await ResolverAsync(f, ct);
        var raw = await (from cr in _sdm.CallReqs.AsNoTracking()
                         where alcance.Contains(cr.category!) && cr.open_date >= desde && (hasta == null || cr.open_date < hasta)
                         group cr by cr.category into g
                         select new { category = g.Key, Total = g.Count(), Violados = g.Count(x => x.sla_violation == 1), Ok = g.Count(x => x.sla_violation == null || x.sla_violation == 0) }).ToListAsync(ct);
        var catMap = await CatMapAsync(ct);
        return raw.Select(r => new CumplimientoSlaDto
        {
            Categoria = r.category != null && catMap.TryGetValue(r.category, out var c) ? c ?? r.category : r.category ?? "",
            TotalTickets = r.Total, SlaCumplido = r.Ok, SlaIncumplido = r.Violados, SlaEnCurso = 0,
            PctCumplimiento = r.Total > 0 ? Math.Round(100m * r.Ok / r.Total, 2) : 0
        }).ToList();
    }

    public async Task<IReadOnlyList<ProductividadDto>> ProductividadAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var (alcance, desde, hasta) = await ResolverAsync(f, ct);
        var raw = await (from cr in _sdm.CallReqs.AsNoTracking()
                         where alcance.Contains(cr.category!) && cr.open_date >= desde && (hasta == null || cr.open_date < hasta) && cr.assignee != null
                         group cr by new { cr.assignee, cr.group_id } into g
                         select new { g.Key.assignee, g.Key.group_id, Total = g.Count(), Resueltos = g.Count(x => x.active_flag == 0), Violados = g.Count(x => x.sla_violation == 1), ConCambio = g.Count(x => x.change != null && x.change > 0), SumaHoras = g.Where(x => x.close_date != null && x.close_date > 0 && x.open_date != null).Sum(x => (double?)(x.close_date!.Value - x.open_date!.Value) / 3600.0) ?? 0 }).ToListAsync(ct);
        var contactos = await _catalogo.ContactosAsync(raw.SelectMany(r => new[] { r.assignee, r.group_id }), ct);
        return raw.Select(r =>
        {
            var an = r.assignee != null && contactos.TryGetValue(Convert.ToHexString(r.assignee), out var a) ? a : null;
            var gr = r.group_id != null && contactos.TryGetValue(Convert.ToHexString(r.group_id), out var g) ? g : null;
            return new ProductividadDto
            {
                Grupo = gr?.Apellido ?? "(Sin grupo)",
                Analista = an != null ? $"{an.Apellido}, {an.Nombre}" : "(Sin asignar)",
                TotalAsignados = r.Total, Resueltos = r.Resueltos, ConSlaViolado = r.Violados, GeneraronCambio = r.ConCambio,
                PromedioHorasResolucion = r.Resueltos > 0 ? Math.Round((decimal)(r.SumaHoras / r.Resueltos), 1) : 0
            };
        }).OrderByDescending(x => x.TotalAsignados).ToList();
    }

    public async Task<IReadOnlyList<TendenciaDto>> TendenciasAsync(TicketFiltro f, string granularidad, CancellationToken ct = default)
    {
        var (alcance, desde, hasta) = await ResolverAsync(f, ct);
        // Antes: se traía cada ticket del rango (categoría, fecha, estado) y se agrupaba en C#.
        // Ahora: GROUP BY (día de apertura, categoría) en SQL; a memoria llegan días × categorías.
        var porDia = await (from cr in _sdm.CallReqs.AsNoTracking()
                            where alcance.Contains(cr.category!) && cr.open_date >= desde && (hasta == null || cr.open_date < hasta)
                            group cr by new { cr.category, Dia = Math.Floor((cr.open_date!.Value - OffsetDia0) / 86400.0) } into g
                            select new { g.Key.category, g.Key.Dia, Total = g.Count(), Cerrados = g.Count(x => x.active_flag == 0) }).ToListAsync(ct);
        var catMap = await CatMapAsync(ct);
        string TipoDe(string? cat) => cat != null && catMap.TryGetValue(cat, out var s) && s != null ? (s.Contains(".Problema/Error.") ? "Incidente" : "Solicitud") : "Otro";
        string Periodo(DateTime d) => granularidad.ToLower() switch
        {
            "semana" => $"{System.Globalization.ISOWeek.GetYear(d)}-W{System.Globalization.ISOWeek.GetWeekOfYear(d):D2}",
            "mes" => d.ToString("yyyy-MM"),
            _ => d.ToString("yyyy-MM-dd")
        };
        return porDia.Select(d => new { Fecha = Dia0.AddDays(d.Dia), Tipo = TipoDe(d.category), d.Total, d.Cerrados })
            .GroupBy(x => new { P = Periodo(x.Fecha), x.Tipo })
            .Select(g => new TendenciaDto { Periodo = g.Key.P, Granularidad = granularidad, TipoCaso = g.Key.Tipo, Abiertos = g.Sum(x => x.Total), Cerrados = g.Sum(x => x.Cerrados) })
            .OrderBy(x => x.Periodo).ThenBy(x => x.TipoCaso).ToList();
    }

    public async Task<IReadOnlyList<CruceOrdenCambioDto>> CruceOrdenCambioAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var (alcance, desde, hasta) = await ResolverAsync(f, ct);
        var raw = await (from cr in _sdm.CallReqs.AsNoTracking()
                         join chg in _sdm.Changes.AsNoTracking() on cr.change equals chg.id
                         where alcance.Contains(cr.category!) && cr.open_date >= desde && (hasta == null || cr.open_date < hasta) && cr.change != null && cr.change > 0
                         group new { cr, chg } by cr.category into g
                         select new { category = g.Key, ReqConCambio = g.Count(), Abiertos = g.Count(x => x.chg.close_date == null || x.chg.close_date == 0), Cerrados = g.Count(x => x.chg.close_date > 0), Critico = g.Count(x => x.cr.active_flag == 0 && (x.chg.close_date == null || x.chg.close_date == 0)) }).ToListAsync(ct);
        var catMap = await CatMapAsync(ct);
        return raw.Select(r => new CruceOrdenCambioDto
        {
            Categoria = r.category != null && catMap.TryGetValue(r.category, out var c) ? c ?? r.category : r.category ?? "",
            ReqConCambio = r.ReqConCambio, CambiosAunAbiertos = r.Abiertos, CambiosCerrados = r.Cerrados, ReqCerradoCambioPendiente = r.Critico
        }).ToList();
    }

    /// <summary>
    /// Entradas contra cierres por mes, para tickets y órdenes de cambio.
    /// "Entraron" = apertura en el rango; "se cerraron" = cierre en el rango. Cada conteo es un
    /// GROUP BY por día en SQL (a memoria llegan como mucho días × 4 series) que aquí se suma por mes.
    /// Acotado siempre por open_date (índice): los cierres exigen además una apertura desde la
    /// fecha base (tickets) o desde la ventana de OC (fecha base − días de ventana), así el plan
    /// entra por el rango de apertura y nunca recorre el histórico completo.
    /// </summary>
    public async Task<IReadOnlyList<FlujoMensualDto>> FlujoMensualAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var p = await _parametros.ObtenerAsync(ct);
        var (alcance, desde, hasta) = await ResolverAsync(f, ct);
        var hastaE = hasta ?? EpochRD.FinDelDiaExclusivo(DateTime.Today);
        var baseTickets = EpochRD.InicioDelDia(p.FechaBase);

        // Tickets que entraron (apertura en el rango)
        var tkEntraron = await _sdm.CallReqs.AsNoTracking()
            .Where(cr => alcance.Contains(cr.category!) && cr.open_date >= desde && cr.open_date < hastaE)
            .GroupBy(cr => Math.Floor((cr.open_date!.Value - OffsetDia0) / 86400.0))
            .Select(g => new { Dia = g.Key, N = g.Count() }).ToListAsync(ct);

        // Tickets que se cerraron (cierre en el rango; apertura desde la fecha base)
        var tkCerrados = await _sdm.CallReqs.AsNoTracking()
            .Where(cr => alcance.Contains(cr.category!) && cr.open_date >= baseTickets
                         && cr.close_date >= desde && cr.close_date < hastaE)
            .GroupBy(cr => Math.Floor((cr.close_date!.Value - OffsetDia0) / 86400.0))
            .Select(g => new { Dia = g.Key, N = g.Count() }).ToListAsync(ct);

        // Órdenes de cambio del área (catálogo chgcat en caché)
        var catsOc = (await _catalogo.CategoriasOcAreaAsync(ct)).Keys.ToList();
        var baseOc = EpochRD.ToEpoch(p.FechaBase.AddDays(-p.OcDiasVentana));

        var ocEntraron = await _sdm.Changes.AsNoTracking()
            .Where(c => c.category != null && catsOc.Contains(c.category) && c.open_date >= desde && c.open_date < hastaE)
            .GroupBy(c => Math.Floor((c.open_date!.Value - OffsetDia0) / 86400.0))
            .Select(g => new { Dia = g.Key, N = g.Count() }).ToListAsync(ct);

        var ocCerradas = await _sdm.Changes.AsNoTracking()
            .Where(c => c.category != null && catsOc.Contains(c.category) && c.open_date >= baseOc
                        && c.close_date >= desde && c.close_date < hastaE)
            .GroupBy(c => Math.Floor((c.close_date!.Value - OffsetDia0) / 86400.0))
            .Select(g => new { Dia = g.Key, N = g.Count() }).ToListAsync(ct);

        static string Mes(double dia) => Dia0.AddDays(dia).ToString("yyyy-MM");

        // Todos los meses del rango, aunque alguno no tenga movimiento (el gráfico no salta meses).
        var meses = new SortedDictionary<string, FlujoMensualDto>(StringComparer.Ordinal);
        // El rango de meses sale de las fechas del filtro (no del epoch, que trae el desfase de EpochRD).
        var ini = (f.FechaDesde ?? p.FechaBase).Date;
        var fin = (f.FechaHasta ?? DateTime.Today).Date;
        for (var m = new DateTime(ini.Year, ini.Month, 1); m <= fin; m = m.AddMonths(1))
            meses[m.ToString("yyyy-MM")] = new FlujoMensualDto { Periodo = m.ToString("yyyy-MM") };

        FlujoMensualDto Fila(double dia)
        {
            var k = Mes(dia);
            if (!meses.TryGetValue(k, out var x)) meses[k] = x = new FlujoMensualDto { Periodo = k };
            return x;
        }
        foreach (var d in tkEntraron) Fila(d.Dia).TicketsEntraron += d.N;
        foreach (var d in tkCerrados) Fila(d.Dia).TicketsCerrados += d.N;
        foreach (var d in ocEntraron) Fila(d.Dia).OcEntraron += d.N;
        foreach (var d in ocCerradas) Fila(d.Dia).OcCerradas += d.N;
        return meses.Values.ToList();
    }

    /// <summary>Medianoche de referencia para numerar días en SQL (sin funciones sobre la columna en el WHERE).</summary>
    private static readonly DateTime Dia0 = new(2000, 1, 1);
    private static readonly double OffsetDia0 = (Dia0 - EpochRD.BaseTicket).TotalSeconds;
}
