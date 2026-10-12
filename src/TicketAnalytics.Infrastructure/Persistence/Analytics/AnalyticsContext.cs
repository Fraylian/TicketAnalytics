using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Domain.Entities;

namespace TicketAnalytics.Infrastructure.Persistence.Analytics;

/// <summary>
/// Base LOCAL TicketAnalytics: lectura y escritura. Aqui vive TODO lo que agregan
/// los tecnicos (seguimiento, JIRA, proveedores, evidencias) y los catalogos propios.
/// NUNCA apunta a MDB.
/// </summary>
public class AnalyticsContext : DbContext, IAnalyticsDbContext
{
    public AnalyticsContext(DbContextOptions<AnalyticsContext> options) : base(options) { }

    public DbSet<CategoriaAlcance> CategoriasAlcance => Set<CategoriaAlcance>();
    public DbSet<TicketSnapshot> TicketSnapshots => Set<TicketSnapshot>();
    public DbSet<TicketChangeLog> TicketChangeLogs => Set<TicketChangeLog>();
    public DbSet<TicketAnotacion> TicketAnotaciones => Set<TicketAnotacion>();
    public DbSet<OrdenCambioComplejidad> OrdenCambioComplejidades => Set<OrdenCambioComplejidad>();

    public DbSet<TicketSeguimiento> TicketSeguimientos => Set<TicketSeguimiento>();
    public DbSet<SeguimientoActualizacion> SeguimientoActualizaciones => Set<SeguimientoActualizacion>();
    public DbSet<JiraRelacion> JiraRelaciones => Set<JiraRelacion>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<ReporteProveedor> ReportesProveedor => Set<ReporteProveedor>();
    public DbSet<Evidencia> Evidencias => Set<Evidencia>();
    public DbSet<Tecnico> Tecnicos => Set<Tecnico>();
    public DbSet<ActividadDiaria> ActividadesDiarias => Set<ActividadDiaria>();
    public DbSet<CategoriaReporteVP> CategoriasReporteVP => Set<CategoriaReporteVP>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Carga TODAS las IEntityTypeConfiguration de este ensamblado.
        // Sin filtro de namespace: es lo que garantiza que CategoriaAlcanceConfig
        // (y las demas) se apliquen siempre.
        b.ApplyConfigurationsFromAssembly(typeof(AnalyticsContext).Assembly);
        base.OnModelCreating(b);
    }
}
