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
/// Dashboard y control de vencimientos de Órdenes de Cambio. Reutiliza la lógica
/// de enriquecimiento de TicketQueryService. El vencimiento se calcula desde
/// chg.sched_end_date (respaldo need_by), validado contra MDB real.
/// Estrategia: se cargan las OC del alcance una vez, se enriquecen y TODOS los
/// filtros de vencimiento se aplican en memoria (el vencimiento es derivado, no
/// una columna filtrable en SQL). La paginación se hace tras el filtrado.
/// </summary>
public class OrdenCambioDashboardService : IOrdenCambioDashboardService
{
    private readonly SdmReadContext _sdm;
    private readonly AnalyticsContext _analytics;
    private readonly TicketQueryService _tickets;

    public OrdenCambioDashboardService(SdmReadContext sdm, AnalyticsContext analytics, TicketQueryService tickets)
    {
        _sdm = sdm;
        _analytics = analytics;
        _tickets = tickets;
    }

    // ---- Carga base: OC del alcance enriquecidas, sin paginar ----
    private async Task<List<OrdenCambioConTicketsDto>> CargarEnriquecidasAsync(OrdenCambioFiltro f, CancellationToken ct)
    {
        // OC directo de chg por su categoría (chgcat), como los queries de DWH.
        // No parte de tickets: así trae TODAS las OC del área, tengan o no tickets.
        var codigosCat = await _tickets.ResolverCategoriasOcAsync(f.Categorias, ct);
        if (codigosCat.Count == 0) return new();

        var inicioPedido = f.AperturaDesde ?? new DateTime(2026, 1, 1);
        int desde = EpochRD.ToEpoch(inicioPedido.AddDays(-100));
        int? hasta = f.AperturaHasta.HasValue ? EpochRD.FinDelDiaExclusivo(f.AperturaHasta.Value) : (int?)null;

        var changes = await _sdm.Changes.AsNoTracking()
            .Where(c => c.category != null && codigosCat.Contains(c.category)
                        && c.open_date >= desde && (hasta == null || c.open_date < hasta))
            .ToListAsync(ct);
        if (changes.Count == 0) return new();

        // Tickets asociados a estas OC (para la lista de relacionados)
        var changeIds = changes.Select(c => c.id).ToList();
        var ticketsConOc = new List<CallReq>();
        foreach (var lote in changeIds.Chunk(2000))
        {
            var ids = lote.ToList();
            ticketsConOc.AddRange(await _sdm.CallReqs.AsNoTracking()
                .Where(cr => cr.change != null && ids.Contains(cr.change.Value)).ToListAsync(ct));
        }

        var enriquecidas = await _tickets.ConstruirOcConTicketsAsync(changes, ticketsConOc, ct);

        // Enlazar última gestión desde el seguimiento local (una sola consulta)
        var refs = enriquecidas.Select(o => o.NoOC).ToList();
        var gestiones = await _analytics.TicketSeguimientos.AsNoTracking()
            .Where(s => s.TipoOrigen == "chg" && refs.Contains(s.RefNum))
            .Select(s => new { s.RefNum, s.ActualizadoEn })
            .ToListAsync(ct);
        foreach (var oc in enriquecidas)
        {
            var g = gestiones.FirstOrDefault(x => x.RefNum == oc.NoOC);
            if (g != null)
            {
                oc.UltimaGestion = g.ActualizadoEn;
                oc.DiasDesdeUltimaGestion = (int)Math.Floor((DateTime.UtcNow - g.ActualizadoEn).TotalDays);
            }
        }

        return enriquecidas;
    }

    // ---- Aplica todos los filtros de vencimiento/analista en memoria ----
    private static IEnumerable<OrdenCambioConTicketsDto> AplicarFiltros(IEnumerable<OrdenCambioConTicketsDto> q, OrdenCambioFiltro f)
    {
        if (!string.IsNullOrWhiteSpace(f.NoOC)) q = q.Where(o => o.NoOC.Contains(f.NoOC, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(f.Texto))
            q = q.Where(o => (o.ResumenOC ?? "").Contains(f.Texto, StringComparison.OrdinalIgnoreCase)
                          || (o.DescripcionOC ?? "").Contains(f.Texto, StringComparison.OrdinalIgnoreCase)
                          || o.NoOC.Contains(f.Texto, StringComparison.OrdinalIgnoreCase));
        if (f.Estados is { Count: > 0 }) q = q.Where(o => o.EstadoOC != null && f.Estados.Contains(o.EstadoOC));
        if (!string.IsNullOrWhiteSpace(f.ComplejidadOC)) q = q.Where(o => o.ComplejidadOC == f.ComplejidadOC);
        if (!string.IsNullOrWhiteSpace(f.AsignatarioNombre))
            q = q.Where(o => (o.AsignatarioOC ?? "").Contains(f.AsignatarioNombre, StringComparison.OrdinalIgnoreCase));

        if (f.CierreDesde.HasValue) q = q.Where(o => o.FechaCierreOC >= f.CierreDesde.Value);
        if (f.CierreHasta.HasValue) q = q.Where(o => o.FechaCierreOC <= f.CierreHasta.Value);
        if (f.VencimientoDesde.HasValue) q = q.Where(o => o.FechaVencimientoOC >= f.VencimientoDesde.Value);
        if (f.VencimientoHasta.HasValue) q = q.Where(o => o.FechaVencimientoOC <= f.VencimientoHasta.Value);

        if (f.MesVencimiento.HasValue) q = q.Where(o => o.FechaVencimientoOC.HasValue && o.FechaVencimientoOC.Value.Month == f.MesVencimiento.Value);
        if (f.AnioVencimiento.HasValue) q = q.Where(o => o.FechaVencimientoOC.HasValue && o.FechaVencimientoOC.Value.Year == f.AnioVencimiento.Value);

        if (f.Vencidas == true) q = q.Where(o => o.EstadoVencimiento == "vencida");
        if (f.VencenHoy == true) q = q.Where(o => o.EstadoVencimiento == "vence_hoy");
        if (f.SinFechaVencimiento == true) q = q.Where(o => o.EstadoVencimiento == "sin_fecha");
        if (f.SinAnalista == true) q = q.Where(o => o.SinAnalista);

        if (f.VencenEsteMes == true)
        {
            var hoy = DateTime.Today;
            q = q.Where(o => o.FechaVencimientoOC.HasValue && o.FechaVencimientoOC.Value.Month == hoy.Month && o.FechaVencimientoOC.Value.Year == hoy.Year);
        }
        if (f.VencenProximoMes == true)
        {
            var prox = DateTime.Today.AddMonths(1);
            q = q.Where(o => o.FechaVencimientoOC.HasValue && o.FechaVencimientoOC.Value.Month == prox.Month && o.FechaVencimientoOC.Value.Year == prox.Year);
        }
        if (f.VencenEnDias is { } dias)
            q = q.Where(o => o.DiasRestantes.HasValue && o.DiasRestantes.Value >= 0 && o.DiasRestantes.Value <= dias);

        if (f.SoloAbiertas == true) q = q.Where(o => o.FechaCierreOC == null);
        if (f.SoloCerradas == true) q = q.Where(o => o.FechaCierreOC != null);
        if (f.ConTicketsRelacionados == true) q = q.Where(o => o.CantidadTickets > 0);
        if (f.ConTicketCerradoOcPendiente == true) q = q.Where(o => o.TieneTicketCerradoOcPendiente);
        if (f.MinTickets.HasValue) q = q.Where(o => o.CantidadTickets >= f.MinTickets.Value);
        if (f.MaxTickets.HasValue) q = q.Where(o => o.CantidadTickets <= f.MaxTickets.Value);

        return q;
    }

    private static IEnumerable<OrdenCambioConTicketsDto> Ordenar(IEnumerable<OrdenCambioConTicketsDto> q, OrdenCambioFiltro f)
        => (f.OrderBy?.ToLowerInvariant(), f.OrderDesc) switch
        {
            ("apertura", true) => q.OrderByDescending(o => o.FechaAperturaOC),
            ("apertura", false) => q.OrderBy(o => o.FechaAperturaOC),
            ("noocv", _) or ("nooc", true) => q.OrderByDescending(o => o.NoOC),
            ("nooc", false) => q.OrderBy(o => o.NoOC),
            ("diasrestantes", true) => q.OrderByDescending(o => o.DiasRestantes ?? int.MaxValue),
            ("diasrestantes", false) => q.OrderBy(o => o.DiasRestantes ?? int.MaxValue),
            // Por defecto: vencimiento ascendente (las que vencen primero arriba, sin fecha al final)
            _ => q.OrderBy(o => o.FechaVencimientoOC ?? DateTime.MaxValue)
        };

    public async Task<PagedResult<OrdenCambioConTicketsDto>> BuscarAsync(OrdenCambioFiltro f, CancellationToken ct = default)
    {
        var todas = await CargarEnriquecidasAsync(f, ct);
        var filtradas = AplicarFiltros(todas, f).ToList();
        var total = filtradas.Count;

        var pagina = f.Page < 1 ? 1 : f.Page;
        var tam = f.PageSize is < 1 or > 200 ? 25 : f.PageSize;
        var items = Ordenar(filtradas, f).Skip((pagina - 1) * tam).Take(tam).ToList();

        return new PagedResult<OrdenCambioConTicketsDto> { Items = items, TotalCount = total, Page = pagina, PageSize = tam };
    }

    public async Task<OrdenCambioDashboardDto> DashboardAsync(OrdenCambioFiltro f, CancellationToken ct = default)
    {
        var todas = await CargarEnriquecidasAsync(f, ct);
        var oc = AplicarFiltros(todas, f).ToList();
        var hoy = DateTime.Today;

        var d = new OrdenCambioDashboardDto
        {
            Total = oc.Count,
            Abiertas = oc.Count(o => o.FechaCierreOC == null),
            Cerradas = oc.Count(o => o.FechaCierreOC != null),
            Vencidas = oc.Count(o => o.EstadoVencimiento == "vencida"),
            VencenEsteMes = oc.Count(o => o.FechaVencimientoOC is { } v && v.Month == hoy.Month && v.Year == hoy.Year && o.FechaCierreOC == null),
            VencenProximos7 = oc.Count(o => o.DiasRestantes is >= 0 and <= 7 && o.FechaCierreOC == null),
            SinFechaVencimiento = oc.Count(o => o.EstadoVencimiento == "sin_fecha"),
            SinAnalista = oc.Count(o => o.SinAnalista),
            ConTicketCerradoOcPendiente = oc.Count(o => o.TieneTicketCerradoOcPendiente),
            PctCompletadas = oc.Count == 0 ? 0 : Math.Round(100.0 * oc.Count(o => o.FechaCierreOC != null) / oc.Count, 1),
            PromedioDiasAbiertos = oc.Where(o => o.FechaAperturaOC.HasValue)
                .Select(o => ((o.FechaCierreOC ?? DateTime.Now) - o.FechaAperturaOC!.Value).TotalDays)
                .DefaultIfEmpty(0).Average(),
            PromedioDiasRestantes = oc.Where(o => o.DiasRestantes.HasValue && o.FechaCierreOC == null)
                .Select(o => (double)o.DiasRestantes!.Value).DefaultIfEmpty(0).Average()
        };
        d.PromedioDiasAbiertos = Math.Round(d.PromedioDiasAbiertos, 1);
        d.PromedioDiasRestantes = Math.Round(d.PromedioDiasRestantes, 1);

        d.PorEstado = oc.GroupBy(o => o.EstadoOC ?? "Sin estado").Select(g => new ConteoOc { Etiqueta = g.Key, Cantidad = g.Count() }).OrderByDescending(x => x.Cantidad).ToList();
        d.PorAnalista = oc.GroupBy(o => o.AsignatarioOC ?? "Sin asignar").Select(g => new ConteoOc { Etiqueta = g.Key, Cantidad = g.Count() }).OrderByDescending(x => x.Cantidad).Take(10).ToList();
        d.PorCategoria = oc.GroupBy(o => CortarCat(o.CategoriaOC)).Select(g => new ConteoOc { Etiqueta = g.Key, Cantidad = g.Count() }).OrderByDescending(x => x.Cantidad).ToList();
        d.PorComplejidad = oc.GroupBy(o => o.ComplejidadOC ?? "Sin asignar").Select(g => new ConteoOc { Etiqueta = g.Key, Cantidad = g.Count() }).ToList();

        d.Antiguedad = new()
        {
            new() { Rango = "0-7 días", Cantidad = oc.Count(o => o.FechaCierreOC == null && Dias(o) is >= 0 and <= 7) },
            new() { Rango = "8-15 días", Cantidad = oc.Count(o => o.FechaCierreOC == null && Dias(o) is >= 8 and <= 15) },
            new() { Rango = "16-30 días", Cantidad = oc.Count(o => o.FechaCierreOC == null && Dias(o) is >= 16 and <= 30) },
            new() { Rango = "Más de 30", Cantidad = oc.Count(o => o.FechaCierreOC == null && Dias(o) > 30) }
        };

        d.CreadasVsCerradas = oc.Where(o => o.FechaAperturaOC.HasValue)
            .GroupBy(o => o.FechaAperturaOC!.Value.ToString("yyyy-MM"))
            .Select(g => new TendenciaMesOc { Mes = g.Key, Creadas = g.Count(), Cerradas = g.Count(o => o.FechaCierreOC.HasValue && o.FechaCierreOC.Value.ToString("yyyy-MM") == g.Key) })
            .OrderBy(x => x.Mes).ToList();

        d.VencenPorSemana = oc.Where(o => o.FechaVencimientoOC.HasValue && o.FechaCierreOC == null && o.DiasRestantes >= 0)
            .GroupBy(o => SemanaClave(o.FechaVencimientoOC!.Value))
            .Select(g => new VencimientoSemanaOc { Semana = g.Key, Cantidad = g.Count() }).OrderBy(x => x.Semana).Take(8).ToList();

        d.VencidasPorAnalista = oc.Where(o => o.EstadoVencimiento == "vencida")
            .GroupBy(o => o.AsignatarioOC ?? "Sin asignar").Select(g => new ConteoOc { Etiqueta = g.Key, Cantidad = g.Count() })
            .OrderByDescending(x => x.Cantidad).Take(10).ToList();

        return d;
    }

    public async Task<ControlVencimientosDto> ControlVencimientosAsync(OrdenCambioFiltro f, CancellationToken ct = default)
    {
        var todas = await CargarEnriquecidasAsync(f, ct);
        // Solo OC abiertas con fecha, salvo el conteo "sin fecha"
        var oc = AplicarFiltros(todas, f).ToList();
        var hoy = DateTime.Today;
        var prox = hoy.AddMonths(1);
        var abiertasConFecha = oc.Where(o => o.FechaCierreOC == null && o.FechaVencimientoOC.HasValue).ToList();

        var c = new ControlVencimientosDto
        {
            Vencidas = abiertasConFecha.Count(o => o.DiasRestantes < 0),
            VencenHoy = abiertasConFecha.Count(o => o.DiasRestantes == 0),
            Vencen7 = abiertasConFecha.Count(o => o.DiasRestantes is >= 0 and <= 7),
            Vencen15 = abiertasConFecha.Count(o => o.DiasRestantes is >= 0 and <= 15),
            Vencen30 = abiertasConFecha.Count(o => o.DiasRestantes is >= 0 and <= 30),
            VencenEsteMes = abiertasConFecha.Count(o => o.FechaVencimientoOC!.Value.Month == hoy.Month && o.FechaVencimientoOC.Value.Year == hoy.Year),
            VencenProximoMes = abiertasConFecha.Count(o => o.FechaVencimientoOC!.Value.Month == prox.Month && o.FechaVencimientoOC.Value.Year == prox.Year),
            SinFecha = oc.Count(o => o.FechaCierreOC == null && !o.FechaVencimientoOC.HasValue)
        };

        var relevantes = abiertasConFecha.Where(o => o.DiasRestantes <= 30).ToList();
        c.PorAnalista = relevantes.GroupBy(o => o.AsignatarioOC ?? "Sin asignar").Select(g => new ConteoOc { Etiqueta = g.Key, Cantidad = g.Count() }).OrderByDescending(x => x.Cantidad).Take(10).ToList();
        c.PorEstado = relevantes.GroupBy(o => o.EstadoOC ?? "Sin estado").Select(g => new ConteoOc { Etiqueta = g.Key, Cantidad = g.Count() }).ToList();
        c.PorCategoria = relevantes.GroupBy(o => CortarCat(o.CategoriaOC)).Select(g => new ConteoOc { Etiqueta = g.Key, Cantidad = g.Count() }).OrderByDescending(x => x.Cantidad).ToList();
        c.PorSemana = abiertasConFecha.Where(o => o.DiasRestantes >= 0)
            .GroupBy(o => SemanaClave(o.FechaVencimientoOC!.Value)).Select(g => new VencimientoSemanaOc { Semana = g.Key, Cantidad = g.Count() }).OrderBy(x => x.Semana).Take(8).ToList();

        c.VencenPrimero = abiertasConFecha.OrderBy(o => o.FechaVencimientoOC).Take(20).ToList();
        return c;
    }

    public async Task<IReadOnlyList<string>> AnalistasConOcAsync(OrdenCambioFiltro f, CancellationToken ct = default)
    {
        var todas = await CargarEnriquecidasAsync(f, ct);
        return todas.Where(o => !string.IsNullOrEmpty(o.AsignatarioOC))
            .Select(o => o.AsignatarioOC!).Distinct().OrderBy(x => x).ToList();
    }

    private static int? Dias(OrdenCambioConTicketsDto o)
        => o.FechaAperturaOC.HasValue ? (int)Math.Floor((DateTime.Today - o.FechaAperturaOC.Value.Date).TotalDays) : null;
    private static string CortarCat(string? c)
    {
        if (string.IsNullOrEmpty(c)) return "Sin categoría";
        var p = c.Split('.');
        return p.Length > 0 ? p[^1] : c;
    }
    private static string SemanaClave(DateTime d)
    {
        var jueves = d.AddDays(3 - ((int)d.DayOfWeek + 6) % 7);
        var semana = System.Globalization.ISOWeek.GetWeekOfYear(jueves);
        return $"{jueves:yyyy}-S{semana:00}";
    }
}
