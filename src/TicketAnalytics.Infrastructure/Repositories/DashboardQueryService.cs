using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Dashboard.DTOs;
using TicketAnalytics.Application.Tickets.DTOs;
using TicketAnalytics.Domain.Common;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;

namespace TicketAnalytics.Infrastructure.Repositories;

public class DashboardQueryService : IDashboardQueryService
{
    private readonly SdmReadContext _sdm;
    private readonly AnalyticsContext _analytics;
    public DashboardQueryService(SdmReadContext sdm, AnalyticsContext analytics) { _sdm = sdm; _analytics = analytics; }

    private async Task<(List<string> alcance, int desde, int? hasta)> ResolverAsync(TicketFiltro f, CancellationToken ct)
    {
        var alcance = await _analytics.CategoriasAlcance.AsNoTracking().Where(c => c.Activo).Select(c => c.Persid).ToListAsync(ct);
        if (f.Categorias is { Count: > 0 }) alcance = alcance.Where(p => f.Categorias.Contains(p)).ToList();
        int desde = f.FechaDesde.HasValue ? EpochRD.InicioDelDia(f.FechaDesde.Value) : EpochRD.ToEpoch(new DateTime(2026, 1, 1));
        int? hasta = f.FechaHasta.HasValue ? EpochRD.FinDelDiaExclusivo(f.FechaHasta.Value) : (int?)null;
        return (alcance, desde, hasta);
    }

    public async Task<IReadOnlyList<VolumenPorEstadoDto>> VolumenPorEstadoAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var (alcance, desde, hasta) = await ResolverAsync(f, ct);
        var raw = await (from cr in _sdm.CallReqs.AsNoTracking()
                         where alcance.Contains(cr.category!) && cr.open_date >= desde && (hasta == null || cr.open_date < hasta)
                         group cr by new { cr.category, cr.status } into g
                         select new { g.Key.category, g.Key.status, Total = g.Count(), Abiertos = g.Count(x => x.active_flag == 1), Cerrados = g.Count(x => x.active_flag == 0) }).ToListAsync(ct);
        var cats = raw.Select(r => r.category).Distinct().ToList();
        var estados = raw.Select(r => r.status).Distinct().ToList();
        var catMap = await _sdm.ProbCtgs.AsNoTracking().Where(p => cats.Contains(p.persid)).ToDictionaryAsync(p => p.persid, p => p.sym, ct);
        var estMap = await _sdm.CrStats.AsNoTracking().Where(s => estados.Contains(s.code)).ToDictionaryAsync(s => s.code, s => s.description ?? s.sym, ct);
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
        var cats = raw.Select(r => r.category).Distinct().ToList();
        var catMap = await _sdm.ProbCtgs.AsNoTracking().Where(p => cats.Contains(p.persid)).ToDictionaryAsync(p => p.persid, p => p.sym, ct);
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
        var uuids = raw.SelectMany(r => new[] { r.assignee, r.group_id }).Where(b => b != null).Select(b => b!).ToList();
        var contactos = await _sdm.CaContacts.AsNoTracking().Where(c => uuids.Contains(c.contact_uuid)).ToListAsync(ct);
        return raw.Select(r =>
        {
            var an = r.assignee != null ? contactos.FirstOrDefault(c => c.contact_uuid.SequenceEqual(r.assignee)) : null;
            var gr = r.group_id != null ? contactos.FirstOrDefault(c => c.contact_uuid.SequenceEqual(r.group_id)) : null;
            return new ProductividadDto
            {
                Grupo = gr?.last_name ?? "(Sin grupo)",
                Analista = an != null ? $"{an.last_name}, {an.first_name}" : "(Sin asignar)",
                TotalAsignados = r.Total, Resueltos = r.Resueltos, ConSlaViolado = r.Violados, GeneraronCambio = r.ConCambio,
                PromedioHorasResolucion = r.Resueltos > 0 ? Math.Round((decimal)(r.SumaHoras / r.Resueltos), 1) : 0
            };
        }).OrderByDescending(x => x.TotalAsignados).ToList();
    }

    public async Task<IReadOnlyList<TendenciaDto>> TendenciasAsync(TicketFiltro f, string granularidad, CancellationToken ct = default)
    {
        var (alcance, desde, hasta) = await ResolverAsync(f, ct);
        var datos = await (from cr in _sdm.CallReqs.AsNoTracking()
                           where alcance.Contains(cr.category!) && cr.open_date >= desde && (hasta == null || cr.open_date < hasta)
                           select new { cr.category, cr.open_date, cr.active_flag }).ToListAsync(ct);
        var cats = datos.Select(d => d.category).Distinct().ToList();
        var catMap = await _sdm.ProbCtgs.AsNoTracking().Where(p => cats.Contains(p.persid)).ToDictionaryAsync(p => p.persid, p => p.sym, ct);
        string TipoDe(string? cat) => cat != null && catMap.TryGetValue(cat, out var s) && s != null ? (s.Contains(".Problema/Error.") ? "Incidente" : "Solicitud") : "Otro";
        string Periodo(DateTime d) => granularidad.ToLower() switch
        {
            "semana" => $"{System.Globalization.ISOWeek.GetYear(d)}-W{System.Globalization.ISOWeek.GetWeekOfYear(d):D2}",
            "mes" => d.ToString("yyyy-MM"),
            _ => d.ToString("yyyy-MM-dd")
        };
        return datos.Select(d => new { Fecha = EpochRD.ToDateTime(d.open_date), Tipo = TipoDe(d.category), d.active_flag })
            .Where(x => x.Fecha.HasValue)
            .GroupBy(x => new { P = Periodo(x.Fecha!.Value), x.Tipo })
            .Select(g => new TendenciaDto { Periodo = g.Key.P, Granularidad = granularidad, TipoCaso = g.Key.Tipo, Abiertos = g.Count(), Cerrados = g.Count(x => x.active_flag == 0) })
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
        var cats = raw.Select(r => r.category).Distinct().ToList();
        var catMap = await _sdm.ProbCtgs.AsNoTracking().Where(p => cats.Contains(p.persid)).ToDictionaryAsync(p => p.persid, p => p.sym, ct);
        return raw.Select(r => new CruceOrdenCambioDto
        {
            Categoria = r.category != null && catMap.TryGetValue(r.category, out var c) ? c ?? r.category : r.category ?? "",
            ReqConCambio = r.ReqConCambio, CambiosAunAbiertos = r.Abiertos, CambiosCerrados = r.Cerrados, ReqCerradoCambioPendiente = r.Critico
        }).ToList();
    }
}
