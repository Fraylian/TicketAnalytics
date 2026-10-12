using TicketAnalytics.Application.Analistas.DTOs;
using TicketAnalytics.Application.Tickets.DTOs;

namespace TicketAnalytics.Application.Common.Interfaces;

public interface IAnalistaService
{
    Task<IReadOnlyList<MetricaAnalistaDto>> MetricasAsync(TicketFiltro f, CancellationToken ct = default);
    Task<MetricaAnalistaDto?> MetricaDeAnalistaAsync(string uuidHex, TicketFiltro f, CancellationToken ct = default);
}
