using TicketAnalytics.Application.Proveedores.DTOs;

namespace TicketAnalytics.Application.Common.Interfaces;

public interface IProveedorService
{
    Task<IReadOnlyList<ProveedorDto>> ListarAsync(CancellationToken ct = default);
    Task<ProveedorDto> CrearAsync(string nombre, string? contacto, string? email, CancellationToken ct = default);
    Task<ReporteProveedorDto> ReportarAsync(string refNum, ReporteProveedorDto req, CancellationToken ct = default);
    Task<IReadOnlyList<ReporteProveedorDto>> ReportesDeSeguimientoAsync(string refNum, CancellationToken ct = default);
    Task<IReadOnlyList<ReporteProveedorDto>> ReportesQueNecesitanSeguimientoAsync(CancellationToken ct = default);

    // ===== Matriz mensual de proveedores =====
    /// <summary>Reportes filtrados (mes calendario, proveedor, responsable…), con estado de SLA calculado.</summary>
    Task<IReadOnlyList<ReporteProveedorMatrizDto>> MatrizBuscarAsync(ReporteProveedorFiltro f, CancellationToken ct = default);
    /// <summary>KPIs de la matriz para el filtro dado.</summary>
    Task<MatrizProveedorResumenDto> MatrizResumenAsync(ReporteProveedorFiltro f, CancellationToken ct = default);
    /// <summary>Crea o edita un reporte de proveedor (con o sin ticket del sistema).</summary>
    Task<ReporteProveedorMatrizDto> MatrizGuardarAsync(GuardarReporteProveedorRequest req, CancellationToken ct = default);
    /// <summary>Elimina un reporte de proveedor.</summary>
    Task MatrizEliminarAsync(long reporteId, CancellationToken ct = default);
    /// <summary>Responsables (asignatarios) distintos que han registrado reportes.</summary>
    Task<IReadOnlyList<string>> MatrizResponsablesAsync(CancellationToken ct = default);

    /// <summary>Reportes de proveedor ligados a un ticket/OC del sistema (por su ref_num).</summary>
    Task<IReadOnlyList<ReporteProveedorMatrizDto>> MatrizPorRefNumSistemaAsync(string refNum, CancellationToken ct = default);

    // ===== CRUD de catálogo de proveedores =====
    Task<IReadOnlyList<ProveedorCatalogoDto>> CatalogoListarAsync(CancellationToken ct = default);
    Task<ProveedorCatalogoDto> CatalogoGuardarAsync(GuardarProveedorRequest req, CancellationToken ct = default);
    Task CatalogoEliminarAsync(int proveedorId, CancellationToken ct = default);
}
