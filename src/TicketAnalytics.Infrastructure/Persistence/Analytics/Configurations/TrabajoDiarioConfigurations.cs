using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketAnalytics.Domain.Entities;

namespace TicketAnalytics.Infrastructure.Persistence.Analytics.Configurations;

public class TecnicoConfig : IEntityTypeConfiguration<Tecnico>
{
    public void Configure(EntityTypeBuilder<Tecnico> e)
    {
        e.ToTable("Tecnico", "dbo");
        e.HasKey(x => x.TecnicoId);
        e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        e.Property(x => x.ContactUuid).HasColumnType("binary(16)");
        e.HasIndex(x => x.Nombre).IsUnique();
        e.HasMany(x => x.Actividades).WithOne(a => a.Tecnico!).HasForeignKey(a => a.TecnicoId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ActividadDiariaConfig : IEntityTypeConfiguration<ActividadDiaria>
{
    public void Configure(EntityTypeBuilder<ActividadDiaria> e)
    {
        e.ToTable("ActividadDiaria", "dbo");
        e.HasKey(x => x.ActividadId);
        e.Property(x => x.Fecha).IsRequired();
        e.Property(x => x.Actividad).IsRequired();
        e.Property(x => x.Tipo).HasConversion<int>();
        e.Property(x => x.NoSolicitud).HasMaxLength(60);
        e.Property(x => x.RefNumVinculado).HasMaxLength(20);
        e.Property(x => x.TipoVinculo).HasMaxLength(5);
        e.Property(x => x.Proyecto).HasMaxLength(120);
        e.Property(x => x.Horas).HasColumnType("decimal(5,2)");
        e.Property(x => x.Estado).HasConversion<int>();
        e.Property(x => x.CreadoPor).HasMaxLength(100);
        e.Property(x => x.ModificadoPor).HasMaxLength(100);
        e.HasIndex(x => x.Fecha);
        e.HasIndex(x => x.TecnicoId);
        e.HasIndex(x => x.RefNumVinculado);

        // Clasificación en conjunto con la gestión (sistema → proveedor → tipo de solución).
        e.Property(x => x.TipoSolucion).HasConversion<int>();
        e.HasIndex(x => x.SistemaId);
        e.HasOne(x => x.Sistema).WithMany().HasForeignKey(x => x.SistemaId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Proveedor).WithMany().HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.Restrict);
    }
}
