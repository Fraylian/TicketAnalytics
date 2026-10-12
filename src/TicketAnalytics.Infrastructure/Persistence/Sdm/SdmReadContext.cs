using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Infrastructure.Persistence.Sdm.Entities;

namespace TicketAnalytics.Infrastructure.Persistence.Sdm;

/// <summary>
/// Contexto de SOLO LECTURA sobre la base [MDB] de CA Service Desk Manager.
///
/// CUMPLIMIENTO DE LA ESPECIFICACION:
///  - Prohibido INSERT/UPDATE/DELETE/ALTER/CREATE/migraciones/EnsureCreated sobre MDB.
///  - SaveChanges y SaveChangesAsync lanzan excepcion (bloqueo duro).
///  - QueryTrackingBehavior = NoTracking global.
///  - AutoDetectChanges deshabilitado.
///  - Solo se mapean tablas existentes de MDB; no se declara ninguna migracion aqui.
/// </summary>
public class SdmReadContext : DbContext
{
    public SdmReadContext(DbContextOptions<SdmReadContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    public DbSet<CallReq> CallReqs => Set<CallReq>();
    public DbSet<Chg> Changes => Set<Chg>();
    public DbSet<AttachedSla> AttachedSlas => Set<AttachedSla>();
    public DbSet<ProbCtg> ProbCtgs => Set<ProbCtg>();
    public DbSet<CrStat> CrStats => Set<CrStat>();
    public DbSet<ChgStat> ChgStats => Set<ChgStat>();
    public DbSet<ChgCat> ChgCats => Set<ChgCat>();
    public DbSet<CaContact> CaContacts => Set<CaContact>();
    public DbSet<GrpMem> GrpMems => Set<GrpMem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(SdmReadContext).Assembly,
            t => t.Namespace?.Contains("Sdm.Configurations") == true);
    }

    // ----- BLOQUEO DURO DE ESCRITURA SOBRE MDB -----
    private static InvalidOperationException Bloqueo()
        => new("SdmReadContext es de SOLO LECTURA. Prohibido escribir/alterar la base MDB de CA SDM.");

    public override int SaveChanges() => throw Bloqueo();
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw Bloqueo();
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw Bloqueo();
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) => throw Bloqueo();
}
