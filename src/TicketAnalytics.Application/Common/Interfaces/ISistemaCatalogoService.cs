using TicketAnalytics.Application.Sistemas.DTOs;

namespace TicketAnalytics.Application.Common.Interfaces;

/// <summary>Catálogos locales de la clasificación: sistemas (con proveedor) y tipos de corrección.</summary>
public interface ISistemaCatalogoService
{
    Task<IReadOnlyList<SistemaDto>> ListarSistemasAsync(CancellationToken ct = default);
    Task<SistemaDto> GuardarSistemaAsync(GuardarSistemaRequest req, CancellationToken ct = default);
    Task<ResultadoEliminacionDto> EliminarSistemaAsync(int sistemaId, CancellationToken ct = default);

    Task<IReadOnlyList<TipoCorreccionDto>> ListarTiposCorreccionAsync(CancellationToken ct = default);
    Task<TipoCorreccionDto> GuardarTipoCorreccionAsync(GuardarTipoCorreccionRequest req, CancellationToken ct = default);
    Task<ResultadoEliminacionDto> EliminarTipoCorreccionAsync(int id, CancellationToken ct = default);

    /// <summary>Opciones ACTIVAS para los selectores de gestión y actividades.</summary>
    Task<CatalogosGestionDto> CatalogosGestionAsync(CancellationToken ct = default);
}
