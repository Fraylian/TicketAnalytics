using TicketAnalytics.Application.Common.Models;
using TicketAnalytics.Application.Dashboard.DTOs;

namespace TicketAnalytics.Application.Common.Interfaces;

/// <summary>Indicadores derivados de la clasificación estructurada de la gestión.</summary>
public interface IIndicadoresGestionService
{
    Task<IndicadoresGestionDto> ObtenerAsync(DateTime? desde, DateTime? hasta, CancellationToken ct = default);

    /// <summary>Casos marcados "Pendiente" de reportar al proveedor, paginados.</summary>
    Task<PagedResult<CasoProveedorDto>> PendientesReportarAsync(DateTime? desde, DateTime? hasta, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Casos en etapa "En espera de proveedor" de un analista (por uuid o nombre).</summary>
    Task<IReadOnlyList<CasoProveedorDto>> EnEsperaProveedorAsync(string? analistaUuidHex, string? analistaNombre, CancellationToken ct = default);

    /// <summary>Casos clasificados del período para exportar (sistema, proveedor, solución…).</summary>
    Task<IReadOnlyList<CasoClasificadoDto>> CasosClasificadosAsync(DateTime? desde, DateTime? hasta, int? sistemaId, CancellationToken ct = default);
}
