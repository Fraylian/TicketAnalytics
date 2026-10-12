using TicketAnalytics.Application.ReporteVP.DTOs;

namespace TicketAnalytics.Application.Common.Interfaces;

/// <summary>Reporte mensual ejecutivo al VP: casos marcados, agrupados por categoría y estado.</summary>
public interface IReporteVPService
{
    /// <summary>Todo lo que necesita la pantalla en una sola llamada (KPIs, categorías, gráficos, casos).</summary>
    Task<ReporteVPOverviewDto> OverviewAsync(ReporteVPFiltro filtro, CancellationToken ct = default);

    /// <summary>Casos del período aplicando filtros, SIN paginar (para exportar).</summary>
    Task<List<CasoReporteVPDto>> CasosAsync(ReporteVPFiltro filtro, CancellationToken ct = default);

    /// <summary>Agrega o actualiza un caso dentro del reporte. No duplica seguimientos.</summary>
    Task<CasoReporteVPDto> GuardarCasoAsync(GuardarCasoVPRequest req, CancellationToken ct = default);

    /// <summary>Saca un caso del reporte (no borra el seguimiento).</summary>
    Task QuitarCasoAsync(long seguimientoId, CancellationToken ct = default);

    /// <summary>Mueve un caso a otro período.</summary>
    Task MoverPeriodoAsync(long seguimientoId, string periodo, CancellationToken ct = default);

    /// <summary>Catálogo de categorías del reporte.</summary>
    Task<IReadOnlyList<CategoriaVPDto>> CategoriasAsync(bool soloActivas = true, CancellationToken ct = default);

    /// <summary>Períodos que ya tienen casos (para el selector).</summary>
    Task<IReadOnlyList<string>> PeriodosDisponiblesAsync(CancellationToken ct = default);
}
