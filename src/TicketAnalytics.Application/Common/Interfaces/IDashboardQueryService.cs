using TicketAnalytics.Application.Dashboard.DTOs;
using TicketAnalytics.Application.Tickets.DTOs;

namespace TicketAnalytics.Application.Common.Interfaces;

public interface IDashboardQueryService
{
    Task<IReadOnlyList<VolumenPorEstadoDto>> VolumenPorEstadoAsync(TicketFiltro f, CancellationToken ct = default);
    Task<IReadOnlyList<CumplimientoSlaDto>> CumplimientoSlaAsync(TicketFiltro f, CancellationToken ct = default);
    Task<IReadOnlyList<ProductividadDto>> ProductividadAsync(TicketFiltro f, CancellationToken ct = default);
    Task<IReadOnlyList<TendenciaDto>> TendenciasAsync(TicketFiltro f, string granularidad, CancellationToken ct = default);
    Task<IReadOnlyList<CruceOrdenCambioDto>> CruceOrdenCambioAsync(TicketFiltro f, CancellationToken ct = default);
}
