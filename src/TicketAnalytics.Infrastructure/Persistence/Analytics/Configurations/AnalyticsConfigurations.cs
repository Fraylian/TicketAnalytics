using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketAnalytics.Domain.Entities;

namespace TicketAnalytics.Infrastructure.Persistence.Analytics.Configurations;

public class CategoriaAlcanceConfig : IEntityTypeConfiguration<CategoriaAlcance>
{
    public void Configure(EntityTypeBuilder<CategoriaAlcance> e)
    {
        e.ToTable("CategoriaAlcance");
        e.HasKey(x => x.Persid);
        e.Property(x => x.Persid).HasMaxLength(60);
        e.Property(x => x.NombreCategoria).HasMaxLength(200).IsRequired();
        e.Property(x => x.TipoCaso).HasMaxLength(20).IsRequired();
        e.HasData(
            new CategoriaAlcance { Persid = "pcat:405614", NombreCategoria = "Tecnología de la Información.Solicitud.Sistemas.Canales & Medios de Pago", TipoCaso = "Solicitud", Activo = true },
            new CategoriaAlcance { Persid = "pcat:405616", NombreCategoria = "Tecnología de la Información.Solicitud.Sistemas.Gestión e Información", TipoCaso = "Solicitud", Activo = true }
        );
    }
}
public class TicketSnapshotConfig : IEntityTypeConfiguration<TicketSnapshot>
{
    public void Configure(EntityTypeBuilder<TicketSnapshot> e)
    {
        e.ToTable("TicketSnapshot");
        e.HasKey(x => x.SnapshotId);
        e.Property(x => x.RefNum).HasMaxLength(20).IsRequired();
        e.Property(x => x.Persid).HasMaxLength(30).IsRequired();
        e.Property(x => x.Categoria).HasMaxLength(60).IsRequired();
        e.Property(x => x.Estado).HasMaxLength(24);
        e.Property(x => x.AssigneeUuid).HasColumnType("binary(16)");
        e.Property(x => x.GroupUuid).HasColumnType("binary(16)");
        e.Property(x => x.HashEstado).HasColumnType("varbinary(32)");
        e.HasIndex(x => new { x.CrId, x.CapturadoEn });
        e.HasIndex(x => x.RefNum);
    }
}
public class TicketChangeLogConfig : IEntityTypeConfiguration<TicketChangeLog>
{
    public void Configure(EntityTypeBuilder<TicketChangeLog> e)
    {
        e.ToTable("TicketChangeLog");
        e.HasKey(x => x.ChangeLogId);
        e.Property(x => x.RefNum).HasMaxLength(20).IsRequired();
        e.Property(x => x.Campo).HasMaxLength(50).IsRequired();
        e.Property(x => x.ValorAnterior).HasMaxLength(200);
        e.Property(x => x.ValorNuevo).HasMaxLength(200);
        e.HasIndex(x => x.RefNum);
        e.HasIndex(x => x.DetectadoEn);
    }
}
public class TicketAnotacionConfig : IEntityTypeConfiguration<TicketAnotacion>
{
    public void Configure(EntityTypeBuilder<TicketAnotacion> e)
    {
        e.ToTable("TicketAnotacion");
        e.HasKey(x => x.AnotacionId);
        e.Property(x => x.RefNum).HasMaxLength(20).IsRequired();
        e.Property(x => x.Autor).HasMaxLength(100).IsRequired();
        e.Property(x => x.Nota).IsRequired();
        e.HasIndex(x => x.RefNum);
    }
}
public class OrdenCambioComplejidadConfig : IEntityTypeConfiguration<OrdenCambioComplejidad>
{
    public void Configure(EntityTypeBuilder<OrdenCambioComplejidad> e)
    {
        e.ToTable("OrdenCambioComplejidad");
        e.HasKey(x => x.ChgRefNum);
        e.Property(x => x.ChgRefNum).HasMaxLength(20);
        e.Property(x => x.Complejidad).HasConversion<int>();
        e.Property(x => x.AsignadoPor).HasMaxLength(100);
    }
}
