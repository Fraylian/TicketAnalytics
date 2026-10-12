using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Sla.DTOs;
using TicketAnalytics.Application.Tickets.DTOs;
using TicketAnalytics.Domain.Common;
using TicketAnalytics.Domain.Enums;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;

namespace TicketAnalytics.Infrastructure.Repositories;

/// <summary>Calcula SLA con tiempos reales cruzando call_req + attached_sla de mdb.</summary>
public class SlaService : ISlaService
{
    private readonly SdmReadContext _sdm;
    private readonly AnalyticsContext _analytics;
    public SlaService(SdmReadContext sdm, AnalyticsContext analytics) { _sdm = sdm; _analytics = analytics; }

    private async Task<(List<string> alcance, int desde, int? hasta)> ResolverAsync(TicketFiltro f, CancellationToken ct)
    {
        var alcance = await _analytics.CategoriasAlcance.AsNoTracking().Where(c => c.Activo).Select(c => c.Persid).ToListAsync(ct);
        if (f.Categorias is { Count: > 0 }) alcance = alcance.Where(p => f.Categorias.Contains(p)).ToList();
        int desde = f.FechaDesde.HasValue ? EpochRD.InicioDelDia(f.FechaDesde.Value) : EpochRD.ToEpoch(new DateTime(2026, 1, 1));
        int? hasta = f.FechaHasta.HasValue ? EpochRD.FinDelDiaExclusivo(f.FechaHasta.Value) : (int?)null;
        return (alcance, desde, hasta);
    }

    // Trae tickets del alcance + su attached_sla, calcula SLA de cada uno
    private async Task<List<SlaTicketDto>> CalcularTodosAsync(TicketFiltro f, CancellationToken ct)
    {
        var (alcance, desde, hasta) = await ResolverAsync(f, ct);
        var tickets = await _sdm.CallReqs.AsNoTracking()
            .Where(cr => alcance.Contains(cr.category!) && cr.open_date >= desde && (hasta == null || cr.open_date < hasta))
            .Select(cr => new { cr.id, cr.ref_num, cr.open_date, cr.close_date, cr.sla_violation })
            .ToListAsync(ct);

        var crIds = tickets.Select(t => t.id).ToList();
        var slas = await _sdm.AttachedSlas.AsNoTracking()
            .Where(s => s.ticket_type == "cr" && s.del == 0 && crIds.Contains(s.ticket_id))
            .ToListAsync(ct);
        var slaPorTicket = slas.GroupBy(s => s.ticket_id).ToDictionary(g => g.Key, g => g.First());

        var ahora = DateTime.Now;
        var lista = new List<SlaTicketDto>(tickets.Count);
        foreach (var t in tickets)
        {
            slaPorTicket.TryGetValue(t.id, out var sla);
            var r = CalculoSla.Calcular(t.open_date, t.close_date, sla?.time_to_violation, sla?.sla_viol_status, ahora);
            lista.Add(new SlaTicketDto
            {
                NoTicket = t.ref_num,
                EstadoSla = r.EstadoTexto,
                FechaApertura = r.FechaApertura,
                FechaVencimiento = r.FechaVencimiento,
                FechaCierre = r.FechaCierre,
                HorasTranscurridas = r.HorasTranscurridas,
                HorasRestantes = r.HorasRestantes,
                PctConsumido = r.PctConsumido,
                ProximoAVencer = r.ProximoAVencer,
                Vencido = r.Vencido
            });
        }
        return lista;
    }

    public async Task<ResumenSlaDto> ResumenAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var todos = await CalcularTodosAsync(f, ct);
        var conSla = todos.Where(t => t.EstadoSla != "N/A").ToList();
        var dentroSla = conSla.Count(t => t.EstadoSla == "Dentro de SLA");
        var cerradosConHoras = todos.Where(t => t.FechaCierre != null && t.FechaApertura != null)
            .Select(t => (t.FechaCierre!.Value - t.FechaApertura!.Value).TotalHours).ToList();

        return new ResumenSlaDto
        {
            Total = todos.Count,
            DentroSla = dentroSla,
            Vencidos = todos.Count(t => t.Vencido),
            ProximosAVencer = todos.Count(t => t.ProximoAVencer),
            EnCurso = todos.Count(t => t.EstadoSla == "En curso"),
            PctCumplimiento = conSla.Count > 0 ? Math.Round(100m * dentroSla / conSla.Count, 2) : 0,
            PromedioHorasResolucion = cerradosConHoras.Count > 0 ? Math.Round(cerradosConHoras.Average(), 1) : 0
        };
    }

    public async Task<IReadOnlyList<SlaTicketDto>> TicketsProximosAVencerAsync(TicketFiltro f, CancellationToken ct = default)
        => (await CalcularTodosAsync(f, ct)).Where(t => t.ProximoAVencer).OrderBy(t => t.HorasRestantes).ToList();

    public async Task<IReadOnlyList<SlaTicketDto>> TicketsVencidosAsync(TicketFiltro f, CancellationToken ct = default)
        => (await CalcularTodosAsync(f, ct)).Where(t => t.Vencido).OrderByDescending(t => t.PctConsumido).ToList();

    public async Task<SlaTicketDto?> SlaDeTicketAsync(string refNum, CancellationToken ct = default)
    {
        var cr = await _sdm.CallReqs.AsNoTracking().FirstOrDefaultAsync(x => x.ref_num == refNum, ct);
        if (cr is null) return null;
        var sla = await _sdm.AttachedSlas.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ticket_type == "cr" && s.del == 0 && s.ticket_id == cr.id, ct);
        var r = CalculoSla.Calcular(cr.open_date, cr.close_date, sla?.time_to_violation, sla?.sla_viol_status, DateTime.Now);
        return new SlaTicketDto
        {
            NoTicket = cr.ref_num, EstadoSla = r.EstadoTexto,
            FechaApertura = r.FechaApertura, FechaVencimiento = r.FechaVencimiento, FechaCierre = r.FechaCierre,
            HorasTranscurridas = r.HorasTranscurridas, HorasRestantes = r.HorasRestantes,
            PctConsumido = r.PctConsumido, ProximoAVencer = r.ProximoAVencer, Vencido = r.Vencido
        };
    }
}
