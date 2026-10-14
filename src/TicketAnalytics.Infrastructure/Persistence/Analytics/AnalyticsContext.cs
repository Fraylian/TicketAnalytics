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
    public DbSet<ParametroSistema> ParametrosSistema => Set<ParametroSistema>();
    public DbSet<AnalistaGrupo> AnalistasGrupo => Set<AnalistaGrupo>();
    public DbSet<Sistema> Sistemas => Set<Sistema>();
    public DbSet<TipoCorreccion> TiposCorreccion => Set<TipoCorreccion>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Carga las IEntityTypeConfiguration de este ensamblado EXCEPTO las de MDB.
        // Antes no había filtro y el modelo local incluía call_req, chg, attached_sla, etc.:
        // en una base nueva EnsureCreated creaba copias vacías de esas tablas de MDB aquí.
        b.ApplyConfigurationsFromAssembly(typeof(AnalyticsContext).Assembly,
            t => t.Namespace?.Contains("Sdm.Configurations") != true);
        base.OnModelCreating(b);
    }
}
