using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Common.Models;
using TicketAnalytics.Application.Tickets.DTOs;
using TicketAnalytics.Domain.Common;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;
using TicketAnalytics.Infrastructure.Persistence.Sdm.Entities;
using TicketAnalytics.Infrastructure.Repositories;

namespace TicketAnalytics.Infrastructure.Services;

/// <summary>
/// Dashboard, control de vencimientos y listado de Órdenes de Cambio.
///
/// Antes: CargarEnriquecidasAsync traía TODAS las OC de la ventana con sus tickets, contactos y
/// catálogos, y todo se filtraba/contaba en memoria (cada pantalla repetía esa carga completa).
/// Ahora: los filtros, conteos y distribuciones se resuelven con GROUP BY en SQL sobre
/// ConsultaOrdenesCambio; solo se enriquece (tickets, contactos, complejidad) la página que se muestra
/// o las 20 OC de "Vencen primero".
/// </summary>
public class OrdenCambioDashboardService : IOrdenCambioDashboardService
{
    private readonly SdmReadContext _sdm;
    private readonly AnalyticsContext _analytics;
    private readonly TicketQueryService _tickets;
    private readonly ConsultaOrdenesCambio _oc;
    private readonly IParametrosProvider _parametros;

    public OrdenCambioDashboardService(SdmReadContext sdm, AnalyticsContext analytics, TicketQueryService tickets,
                                       ConsultaOrdenesCambio oc, IParametrosProvider parametros)
    {
        _sdm = sdm;
        _analytics = analytics;
        _tickets = tickets;
        _oc = oc;
        _parametros = parametros;
    }

    // ===================== LISTADO =====================

    public async Task<PagedResult<OrdenCambioConTicketsDto>> BuscarAsync(OrdenCambioFiltro f, CancellationToken ct = default)
    {
        var r = await _oc.RelojAsync(ct);
        var q = await _oc.FiltradaAsync(f, r, ct);

        var pagina = f.Page < 1 ? 1 : f.Page;
        // Antes un pedido mayor a 200 caía en silencio a 25 filas; ahora se acota al máximo.
        var tam = Math.Clamp(f.PageSize, 1, 200);

        var total = await q.CountAsync(ct);
        var ids = await ConsultaOrdenesCambio.Ordenar(q, f).Skip((pagina - 1) * tam).Take(tam)
            .Select(o => o.Id).ToListAsync(ct);
        var items = await EnriquecerAsync(ids, ct);

        return new PagedResult<OrdenCambioConTicketsDto> { Items = items, TotalCount = total, Page = pagina, PageSize = tam };
    }

    /// <summary>Exportación: todas las OC del filtro, ordenadas, enriquecidas por lotes.</summary>
    public async Task<List<OrdenCambioConTicketsDto>> BuscarTodasAsync(OrdenCambioFiltro f, CancellationToken ct = default)
    {
        var r = await _oc.RelojAsync(ct);
        var q = await _oc.FiltradaAsync(f, r, ct);
        var ids = await ConsultaOrdenesCambio.Ordenar(q, f).Select(o => o.Id).Take(50_000).ToListAsync(ct);

        var resultado = new List<OrdenCambioConTicketsDto>(ids.Count);
        foreach (var lote in ids.Chunk(500))
            resultado.AddRange(await EnriquecerAsync(lote.ToList(), ct));
        return resultado;
    }

    /// <summary>Carga y enriquece SOLO las OC indicadas, en el orden recibido.</summary>
    private async Task<List<OrdenCambioConTicketsDto>> EnriquecerAsync(List<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();

        // chg por clave primaria (XPKchg) y sus tickets por call_req_x17 (change).
        var changes = await _sdm.Changes.AsNoTracking().Where(c => ids.Contains(c.id)).ToListAsync(ct);
        var tickets = await _sdm.CallReqs.AsNoTracking()
            .Where(cr => cr.change != null && ids.Contains(cr.change.Value)).ToListAsync(ct);

        var dtos = await _tickets.ConstruirOcConTicketsAsync(changes, tickets, ct);
        await CompletarUltimaGestionAsync(dtos, ct);

        var porId = dtos.ToDictionary(d => d.ChgId);
        return ids.Where(porId.ContainsKey).Select(id => porId[id]).ToList();
    }

    private async Task CompletarUltimaGestionAsync(List<OrdenCambioConTicketsDto> dtos, CancellationToken ct)
    {
        var refs = dtos.Select(o => o.NoOC).ToList();
        if (refs.Count == 0) return;
        var gestiones = await _analytics.TicketSeguimientos.AsNoTracking()
            .Where(s => s.TipoOrigen == "chg" && refs.Contains(s.RefNum))
            .Select(s => new { s.RefNum, s.ActualizadoEn })
            .ToListAsync(ct);
        foreach (var oc in dtos)
        {
            var g = gestiones.FirstOrDefault(x => x.RefNum == oc.NoOC);
            if (g != null)
            {
                oc.UltimaGestion = g.ActualizadoEn;
                oc.DiasDesdeUltimaGestion = (int)Math.Floor((DateTime.UtcNow - g.ActualizadoEn).TotalDays);
            }
        }
    }

    // ===================== DASHBOARD =====================

    public async Task<OrdenCambioDashboardDto> DashboardAsync(OrdenCambioFiltro f, CancellationToken ct = default)
    {
        var r = await _oc.RelojAsync(ct);
        var q = await _oc.FiltradaAsync(f, r, ct);
        int hoy = r.HoyE, en8 = r.DiaE(r.P.OcDiasVencePronto + 1);
        int m0 = ConsultaOrdenesCambio.Reloj.Limite(new DateTime(r.Hoy.Year, r.Hoy.Month, 1));
        int m1 = ConsultaOrdenesCambio.Reloj.Limite(new DateTime(r.Hoy.Year, r.Hoy.Month, 1).AddMonths(1));
        double hoyOff = r.HoyOffset;
        int ahora = r.AhoraE;

        // 1 consulta: todos los indicadores numéricos (GROUP BY constante = una sola fila).
        var k = await q.GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(),
            Abiertas = g.Count(o => o.CloseDate == null || o.CloseDate == 0),
            Vencidas = g.Count(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc < hoy),
            VencenEsteMes = g.Count(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc >= m0 && o.Venc < m1),
            VencenProximos7 = g.Count(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc >= hoy && o.Venc < en8),
            SinFecha = g.Count(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc == null),
            SinAnalista = g.Count(o => o.Assignee == null),
            ConApertura = g.Count(o => o.OpenDate != null),
            SumaSegAbiertos = g.Sum(o => o.OpenDate == null ? 0L
                                       : (long)((o.CloseDate == null || o.CloseDate == 0) ? ahora : o.CloseDate!.Value) - o.OpenDate.Value),
            AbiertasConVenc = g.Count(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc != null),
            SumaDiasRestantes = g.Sum(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc != null
                                          ? Math.Floor((o.Venc!.Value - hoyOff) / 86400.0) : 0.0)
        }).FirstOrDefaultAsync(ct);

        // Contado aparte: un EXISTS dentro de un SUM no lo admite SQL Server.
        var conPendiente = await q.Where(o => (o.CloseDate == null || o.CloseDate == 0)
                                              && _sdm.CallReqs.Any(cr => cr.change == o.Id && cr.active_flag == 0))
                                  .CountAsync(ct);

        var d = new OrdenCambioDashboardDto();
        if (k is not null)
        {
            d.Total = k.Total;
            d.Abiertas = k.Abiertas;
            d.Cerradas = k.Total - k.Abiertas;
            d.Vencidas = k.Vencidas;
            d.VencenEsteMes = k.VencenEsteMes;
            d.VencenProximos7 = k.VencenProximos7;
            d.SinFechaVencimiento = k.SinFecha;
            d.SinAnalista = k.SinAnalista;
            d.PctCompletadas = k.Total == 0 ? 0 : Math.Round(100.0 * (k.Total - k.Abiertas) / k.Total, 1);
            d.PromedioDiasAbiertos = k.ConApertura == 0 ? 0 : Math.Round(k.SumaSegAbiertos / 86400.0 / k.ConApertura, 1);
            d.PromedioDiasRestantes = k.AbiertasConVenc == 0 ? 0 : Math.Round(k.SumaDiasRestantes / k.AbiertasConVenc, 1);
        }
        d.ConTicketCerradoOcPendiente = conPendiente;

        var textoEstado = await _oc.TextoEstadoAsync(ct);
        var categoria = await _oc.CategoriaLegibleAsync(ct);

        // Distribuciones: GROUP BY en SQL, nombres desde la caché.
        var porEstado = await q.GroupBy(o => o.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        d.PorEstado = porEstado.GroupBy(x => textoEstado(x.Key)).Select(g => new ConteoOc { Etiqueta = g.Key, Cantidad = g.Sum(x => x.N) })
            .OrderByDescending(x => x.Cantidad).ToList();

        d.PorAnalista = await ConteoPorAnalistaAsync(q, 10, ct);

        var porCat = await q.GroupBy(o => o.Category).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        d.PorCategoria = porCat.GroupBy(x => CortarCat(categoria(x.Key))).Select(g => new ConteoOc { Etiqueta = g.Key, Cantidad = g.Sum(x => x.N) })
            .OrderByDescending(x => x.Cantidad).ToList();

        var porNivel = await q.GroupBy(o => o.Nivel).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        d.PorComplejidad = porNivel.Select(x => new ConteoOc { Etiqueta = ConsultaOrdenesCambio.EtiquetaNivel(x.Key), Cantidad = x.N }).ToList();

        // Antigüedad de las abiertas: días = CEILING((hoy - apertura) / 1 día) (igual que hoy - apertura.Date).
        var ant = await q.Where(o => (o.CloseDate == null || o.CloseDate == 0) && o.OpenDate != null)
            .Select(o => Math.Ceiling((hoyOff - o.OpenDate!.Value) / 86400.0))
            .GroupBy(dias => dias).Select(g => new { Dias = g.Key, N = g.Count() }).ToListAsync(ct);
        int Rango(double a, double b) => ant.Where(x => x.Dias >= a && x.Dias <= b).Sum(x => x.N);
        d.Antiguedad = new()
        {
            new() { Rango = "0-7 días", Cantidad = Rango(0, 7) },
            new() { Rango = "8-15 días", Cantidad = Rango(8, 15) },
            new() { Rango = "16-30 días", Cantidad = Rango(16, 30) },
            new() { Rango = "Más de 30", Cantidad = ant.Where(x => x.Dias > 30).Sum(x => x.N) }
        };

        // Creadas vs cerradas por mes: SQL agrupa por (día de apertura, día de cierre); C# junta en meses.
        var dias = await q.Where(o => o.OpenDate != null)
            .Select(o => new
            {
                A = Math.Floor((o.OpenDate!.Value - OffsetDia0) / 86400.0),
                C = (o.CloseDate == null || o.CloseDate == 0) ? (double?)null : Math.Floor((o.CloseDate!.Value - OffsetDia0) / 86400.0)
            })
            .GroupBy(x => new { x.A, x.C }).Select(g => new { g.Key.A, g.Key.C, N = g.Count() }).ToListAsync(ct);
        d.CreadasVsCerradas = dias
            .GroupBy(x => Mes(x.A))
            .Select(g => new TendenciaMesOc { Mes = g.Key, Creadas = g.Sum(x => x.N), Cerradas = g.Where(x => x.C.HasValue && Mes(x.C.Value) == g.Key).Sum(x => x.N) })
            .OrderBy(x => x.Mes).ToList();

        // Vencen por semana (abiertas, desde hoy): SQL agrupa por día de vencimiento; C# arma la semana ISO.
        var porDiaVenc = await q.Where(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc >= hoy)
            .Select(o => Math.Floor((o.Venc!.Value - OffsetDia0) / 86400.0))
            .GroupBy(x => x).Select(g => new { Dia = g.Key, N = g.Count() }).ToListAsync(ct);
        d.VencenPorSemana = porDiaVenc.GroupBy(x => SemanaClave(Dia0.AddDays(x.Dia)))
            .Select(g => new VencimientoSemanaOc { Semana = g.Key, Cantidad = g.Sum(x => x.N) })
            .OrderBy(x => x.Semana).Take(8).ToList();

        d.VencidasPorAnalista = await ConteoPorAnalistaAsync(
            q.Where(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc < hoy), 10, ct);

        return d;
    }

    // ===================== CONTROL DE VENCIMIENTOS =====================

    public async Task<ControlVencimientosDto> ControlVencimientosAsync(OrdenCambioFiltro f, CancellationToken ct = default)
    {
        var r = await _oc.RelojAsync(ct);
        var q = await _oc.FiltradaAsync(f, r, ct);
        var abiertasConFecha = q.Where(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc != null);

        int hoy = r.HoyE, d1 = r.DiaE(1), d8 = r.DiaE(8), d16 = r.DiaE(16), d31 = r.DiaE(31);
        var mes0 = new DateTime(r.Hoy.Year, r.Hoy.Month, 1);
        int m0 = ConsultaOrdenesCambio.Reloj.Limite(mes0), m1 = ConsultaOrdenesCambio.Reloj.Limite(mes0.AddMonths(1)),
            m2 = ConsultaOrdenesCambio.Reloj.Limite(mes0.AddMonths(2));

        var k = await abiertasConFecha.GroupBy(_ => 1).Select(g => new
        {
            Vencidas = g.Count(o => o.Venc < hoy),
            VencenHoy = g.Count(o => o.Venc >= hoy && o.Venc < d1),
            Vencen7 = g.Count(o => o.Venc >= hoy && o.Venc < d8),
            Vencen15 = g.Count(o => o.Venc >= hoy && o.Venc < d16),
            Vencen30 = g.Count(o => o.Venc >= hoy && o.Venc < d31),
            EsteMes = g.Count(o => o.Venc >= m0 && o.Venc < m1),
            ProximoMes = g.Count(o => o.Venc >= m1 && o.Venc < m2)
        }).FirstOrDefaultAsync(ct);

        var c = new ControlVencimientosDto
        {
            Vencidas = k?.Vencidas ?? 0,
            VencenHoy = k?.VencenHoy ?? 0,
            Vencen7 = k?.Vencen7 ?? 0,
            Vencen15 = k?.Vencen15 ?? 0,
            Vencen30 = k?.Vencen30 ?? 0,
            VencenEsteMes = k?.EsteMes ?? 0,
            VencenProximoMes = k?.ProximoMes ?? 0,
            SinFecha = await q.CountAsync(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc == null, ct)
        };

        // "Relevantes": abiertas con fecha que vencen en 30 días o menos (incluye las ya vencidas).
        var relevantes = abiertasConFecha.Where(o => o.Venc < d31);
        var textoEstado = await _oc.TextoEstadoAsync(ct);
        var categoria = await _oc.CategoriaLegibleAsync(ct);

        c.PorAnalista = await ConteoPorAnalistaAsync(relevantes, 10, ct);
        var porEstado = await relevantes.GroupBy(o => o.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        c.PorEstado = porEstado.GroupBy(x => textoEstado(x.Key)).Select(g => new ConteoOc { Etiqueta = g.Key, Cantidad = g.Sum(x => x.N) }).ToList();
        var porCat = await relevantes.GroupBy(o => o.Category).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        c.PorCategoria = porCat.GroupBy(x => CortarCat(categoria(x.Key))).Select(g => new ConteoOc { Etiqueta = g.Key, Cantidad = g.Sum(x => x.N) })
            .OrderByDescending(x => x.Cantidad).ToList();

        var porDiaVenc = await abiertasConFecha.Where(o => o.Venc >= hoy)
            .Select(o => Math.Floor((o.Venc!.Value - OffsetDia0) / 86400.0))
            .GroupBy(x => x).Select(g => new { Dia = g.Key, N = g.Count() }).ToListAsync(ct);
        c.PorSemana = porDiaVenc.GroupBy(x => SemanaClave(Dia0.AddDays(x.Dia)))
            .Select(g => new VencimientoSemanaOc { Semana = g.Key, Cantidad = g.Sum(x => x.N) })
            .OrderBy(x => x.Semana).Take(8).ToList();

        // Las 20 que vencen primero: SQL ordena y limita; solo esas 20 se enriquecen.
        var ids = await abiertasConFecha.OrderBy(o => o.Venc).ThenBy(o => o.Id).Take(20).Select(o => o.Id).ToListAsync(ct);
        c.VencenPrimero = await EnriquecerAsync(ids, ct);
        return c;
    }

    public async Task<IReadOnlyList<string>> AnalistasConOcAsync(OrdenCambioFiltro f, CancellationToken ct = default)
    {
        var r = await _oc.RelojAsync(ct);
        // Como antes: todas las OC de la ventana, sin el resto de filtros.
        var q = await _oc.FiltradaAsync(f, r, ct, aplicarFiltros: false);
        var uuids = await q.Where(o => o.Assignee != null).Select(o => o.Assignee).Distinct().ToListAsync(ct);
        var nombre = await _oc.NombresAsync(uuids, ct);
        return uuids.Select(nombre).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).Distinct().OrderBy(x => x).ToList();
    }

    // ===================== ATENCIÓN POR PERSONA =====================

    /// <summary>
    /// "⚠ Fulano — N OC vencen en el mes · X vencidas · Y esta semana", más sus tickets pendientes
    /// de gestión. Conteos por persona con GROUP BY en SQL sobre las OC ABIERTAS del área.
    /// </summary>
    public async Task<AtencionPorPersonaDto> AtencionPorPersonaAsync(int anio, int mes, CancellationToken ct = default)
    {
        var r = await _oc.RelojAsync(ct);
        var p = r.P;
        var cats = await _oc.CategoriasAsync(null, ct);
        var grupo = await _tickets.GrupoAsync(ct);

        var mesInicio = new DateTime(anio, mes, 1);
        int m0 = ConsultaOrdenesCambio.Reloj.Limite(mesInicio), m1 = ConsultaOrdenesCambio.Reloj.Limite(mesInicio.AddMonths(1));
        int hoy = r.HoyE, manana = r.DiaE(1);
        // Semana calendario: de hoy hasta el domingo (inclusive).
        var finSemana = r.Hoy.AddDays(7 - ((int)r.Hoy.DayOfWeek == 0 ? 7 : (int)r.Hoy.DayOfWeek) + 1);
        int semana = ConsultaOrdenesCambio.Reloj.Limite(finSemana);

        // Ventana: abiertas desde la fecha base (menos la ventana de plazos), para no perder vencidas viejas.
        var (desde, _) = ConsultaOrdenesCambio.Ventana(r, null, null);
        var abiertas = (await _oc.BaseAsync(r, cats, desde, null,
                q => q.Where(c => c.close_date == null || c.close_date == 0), ct));

        var porPersona = await abiertas.GroupBy(o => o.Assignee).Select(g => new
        {
            Uuid = g.Key,
            Abiertas = g.Count(),
            Vencidas = g.Count(o => o.Venc < hoy),
            VencenHoy = g.Count(o => o.Venc >= hoy && o.Venc < manana),
            VencenSemana = g.Count(o => o.Venc >= hoy && o.Venc < semana),
            VencenMes = g.Count(o => o.Venc >= m0 && o.Venc < m1)
        }).ToListAsync(ct);

        var tk = await _tickets.PendientesPorPersonaAsync(grupo, ct);

        var resultado = new AtencionPorPersonaDto
        {
            Anio = anio,
            Mes = mes,
            Periodo = $"{CultureInfo.GetCultureInfo("es-DO").DateTimeFormat.GetMonthName(mes)} {anio}",
            DiasSinAvance = p.SeguimientoDiasSinAvance,
            GeneradoEn = DateTime.Now
        };

        foreach (var m in grupo)
        {
            var oc = porPersona.FirstOrDefault(x => x.Uuid != null && x.Uuid.SequenceEqual(m.Uuid));
            tk.TryGetValue(m.UuidHex, out var t);
            resultado.Personas.Add(new AtencionPersonaDto
            {
                Analista = m.Nombre,
                UserId = m.UserId,
                Uuid = m.UuidHex,
                OcAbiertas = oc?.Abiertas ?? 0,
                OcVencidas = oc?.Vencidas ?? 0,
                OcVencenHoy = oc?.VencenHoy ?? 0,
                OcVencenSemana = oc?.VencenSemana ?? 0,
                OcVencenMes = oc?.VencenMes ?? 0,
                TicketsAbiertos = t?.Abiertos ?? 0,
                TicketsSlaVencido = t?.SlaVencido ?? 0,
                TicketsSinGestion = t?.SinGestion ?? 0,
                TicketsSinAvance = t?.SinAvance ?? 0
            });
        }

        var sinAnalista = porPersona.FirstOrDefault(x => x.Uuid == null);
        if (sinAnalista is { Abiertas: > 0 })
            resultado.Personas.Add(new AtencionPersonaDto
            {
                Analista = "Sin asignar",
                OcAbiertas = sinAnalista.Abiertas,
                OcVencidas = sinAnalista.Vencidas,
                OcVencenHoy = sinAnalista.VencenHoy,
                OcVencenSemana = sinAnalista.VencenSemana,
                OcVencenMes = sinAnalista.VencenMes
            });

        foreach (var x in resultado.Personas)
            x.Nivel = x.OcVencidas > 0 || x.OcVencenHoy > 0 || x.TicketsSlaVencido > 0 ? "critico"
                    : x.OcVencenSemana > 0 || x.TicketsSinGestion > 0 || x.TicketsSinAvance > 0 ? "atencion"
                    : "ok";

        resultado.Personas = resultado.Personas
            .OrderBy(x => x.Nivel switch { "critico" => 0, "atencion" => 1, _ => 2 })
            .ThenByDescending(x => x.OcVencidas).ThenByDescending(x => x.OcVencenSemana).ThenByDescending(x => x.OcVencenMes)
            .ToList();
        return resultado;
    }

    // ===================== utilidades =====================

    /// <summary>Conteo por analista (GROUP BY assignee en SQL) con nombre desde la caché, top N.</summary>
    private async Task<List<ConteoOc>> ConteoPorAnalistaAsync(IQueryable<FilaOc> q, int top, CancellationToken ct)
    {
        var filas = await q.GroupBy(o => o.Assignee).Select(g => new { Uuid = g.Key, N = g.Count() }).ToListAsync(ct);
        var nombre = await _oc.NombresAsync(filas.Select(x => x.Uuid), ct);
        return filas.GroupBy(x => nombre(x.Uuid) ?? "Sin asignar")
            .Select(g => new ConteoOc { Etiqueta = g.Key, Cantidad = g.Sum(x => x.N) })
            .OrderByDescending(x => x.Cantidad).Take(top).ToList();
    }

    /// <summary>Medianoche de referencia para numerar días en SQL (FLOOR((epoch - offset) / 86400)).</summary>
    private static readonly DateTime Dia0 = new(2000, 1, 1);
    private static readonly double OffsetDia0 = ConsultaOrdenesCambio.Reloj.Offset(Dia0);

    private static string Mes(double dia) => Dia0.AddDays(dia).ToString("yyyy-MM");

    private static string CortarCat(string? c)
    {
        if (string.IsNullOrEmpty(c)) return "Sin categoría";
        var p = c.Split('.');
        return p.Length > 0 ? p[^1] : c;
    }

    private static string SemanaClave(DateTime d)
    {
        var jueves = d.AddDays(3 - ((int)d.DayOfWeek + 6) % 7);
        var semana = ISOWeek.GetWeekOfYear(jueves);
        return $"{jueves:yyyy}-S{semana:00}";
    }
}
