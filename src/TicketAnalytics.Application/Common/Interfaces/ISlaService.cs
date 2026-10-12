using TicketAnalytics.Application.Sla.DTOs;
using TicketAnalytics.Application.Tickets.DTOs;

namespace TicketAnalytics.Application.Common.Interfaces;

public interface ISlaService
{
    Task<ResumenSlaDto> ResumenAsync(TicketFiltro f, CancellationToken ct = default);
    Task<IReadOnlyList<SlaTicketDto>> TicketsProximosAVencerAsync(TicketFiltro f, CancellationToken ct = default);
    Task<IReadOnlyList<SlaTicketDto>> TicketsVencidosAsync(TicketFiltro f, CancellationToken ct = default);
    Task<SlaTicketDto?> SlaDeTicketAsync(string refNum, CancellationToken ct = default);
}
