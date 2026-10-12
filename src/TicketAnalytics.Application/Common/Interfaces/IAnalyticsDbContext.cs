using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Domain.Entities;

namespace TicketAnalytics.Application.Common.Interfaces;

public interface IAnalyticsDbContext
{
    DbSet<CategoriaAlcance> CategoriasAlcance { get; }
    DbSet<TicketSnapshot> TicketSnapshots { get; }
    DbSet<TicketChangeLog> TicketChangeLogs { get; }
    DbSet<TicketAnotacion> TicketAnotaciones { get; }
    DbSet<OrdenCambioComplejidad> OrdenCambioComplejidades { get; }

    // Seguimiento operativo
    DbSet<TicketSeguimiento> TicketSeguimientos { get; }
    DbSet<SeguimientoActualizacion> SeguimientoActualizaciones { get; }
    DbSet<JiraRelacion> JiraRelaciones { get; }
    DbSet<Proveedor> Proveedores { get; }
    DbSet<ReporteProveedor> ReportesProveedor { get; }
    DbSet<Evidencia> Evidencias { get; }
    DbSet<Tecnico> Tecnicos { get; }
    DbSet<ActividadDiaria> ActividadesDiarias { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
