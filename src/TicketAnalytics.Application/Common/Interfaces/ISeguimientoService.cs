using TicketAnalytics.Application.Common.Models;
using TicketAnalytics.Application.Seguimiento.DTOs;
using TicketAnalytics.Application.Tickets.DTOs;

namespace TicketAnalytics.Application.Common.Interfaces;

public interface ISeguimientoService
{
    Task<SeguimientoDto?> ObtenerPorRefNumAsync(string refNum, CancellationToken ct = default);

    /// <summary>Cierra la gestión: el caso pasa a "finalizadas" y deja de contar como activo.</summary>
    Task FinalizarAsync(long seguimientoId, string motivo, string? usuario, CancellationToken ct = default);

    /// <summary>Reabre una gestión finalizada.</summary>
    Task ReabrirAsync(long seguimientoId, string? usuario, CancellationToken ct = default);
    Task<SeguimientoDto> CrearAsync(CrearSeguimientoRequest req, CancellationToken ct = default);
    Task<ActualizacionDto> AgregarActualizacionAsync(string refNum, AgregarActualizacionRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<ActualizacionDto>> HistorialAsync(string refNum, CancellationToken ct = default);
    Task<PagedResult<SeguimientoDto>> BuscarAsync(TicketFiltro f, CancellationToken ct = default);
    /// <summary>Conteos de la bandeja con los mismos filtros de la búsqueda (no solo la página visible).</summary>
    Task<ResumenSeguimientoDto> ResumenAsync(TicketFiltro f, CancellationToken ct = default);
}
