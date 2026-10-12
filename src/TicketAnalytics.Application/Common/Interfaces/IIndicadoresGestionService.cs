using TicketAnalytics.Application.Dashboard.DTOs;

namespace TicketAnalytics.Application.Common.Interfaces;

/// <summary>Indicadores derivados de la clasificación estructurada de la gestión.</summary>
public interface IIndicadoresGestionService
{
    Task<IndicadoresGestionDto> ObtenerAsync(DateTime? desde, DateTime? hasta, CancellationToken ct = default);
}
