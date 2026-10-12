using TicketAnalytics.Application.Common.Models;
using TicketAnalytics.Application.Tickets.DTOs;

namespace TicketAnalytics.Application.Common.Interfaces;

public interface IOrdenCambioDashboardService
{
    /// <summary>Búsqueda avanzada de OC con todos los filtros de vencimiento/analista.</summary>
    Task<PagedResult<OrdenCambioConTicketsDto>> BuscarAsync(OrdenCambioFiltro f, CancellationToken ct = default);
    /// <summary>Todas las OC que cumplen el filtro, ordenadas y SIN paginar. Para exportaciones completas.</summary>
    Task<List<OrdenCambioConTicketsDto>> BuscarTodasAsync(OrdenCambioFiltro f, CancellationToken ct = default);
    /// <summary>KPIs y datos de gráficos del dashboard de OC para el filtro dado.</summary>
    Task<OrdenCambioDashboardDto> DashboardAsync(OrdenCambioFiltro f, CancellationToken ct = default);
    /// <summary>Sección "Control de vencimientos".</summary>
    Task<ControlVencimientosDto> ControlVencimientosAsync(OrdenCambioFiltro f, CancellationToken ct = default);
    /// <summary>Analistas con OC en el alcance (para el selector de filtro).</summary>
    Task<IReadOnlyList<string>> AnalistasConOcAsync(OrdenCambioFiltro f, CancellationToken ct = default);
}
