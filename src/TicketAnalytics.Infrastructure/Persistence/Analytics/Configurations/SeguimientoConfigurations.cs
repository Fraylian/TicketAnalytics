using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketAnalytics.Domain.Entities;
using TicketAnalytics.Domain.Enums;

namespace TicketAnalytics.Infrastructure.Persistence.Analytics.Configurations;

public class TicketSeguimientoConfig : IEntityTypeConfiguration<TicketSeguimiento>
{
    public void Configure(EntityTypeBuilder<TicketSeguimiento> e)
    {
        e.ToTable("TicketSeguimiento");
        e.HasKey(x => x.SeguimientoId);
        e.Property(x => x.RefNum).HasMaxLength(20).IsRequired();
        e.Property(x => x.TipoOrigen).HasMaxLength(5).IsRequired().HasDefaultValue("cr");
        e.Property(x => x.Categoria).HasMaxLength(200);
        e.Property(x => x.TipoCaso).HasMaxLength(20);
        e.Property(x => x.EtapaActual).HasConversion<int>();
        e.Property(x => x.TecnicoResponsable).HasMaxLength(120);
        e.Property(x => x.TecnicoUuid).HasColumnType("binary(16)");
        e.Property(x => x.OrdenCambioRef).HasMaxLength(20);
        e.Property(x => x.CreadoPor).HasMaxLength(100);
        e.HasIndex(x => new { x.RefNum, x.TipoOrigen }).IsUnique();
        e.HasIndex(x => x.EtapaActual);
        e.HasIndex(x => x.Activo);

        // --- Campos de reporte a proveedor y reporte VP ---
        // OJO: nada de HasDefaultValue en estas columnas. Cuando el valor coincide con
        // el default de C# (0 / false), EF lo interpreta como "no asignado", OMITE la
        // columna del INSERT y deja que la base ponga su DEFAULT. Si la columna es
        // NOT NULL y no tiene constraint DEFAULT, el INSERT falla con "Cannot insert
        // the value NULL". Sin HasDefaultValue, EF siempre escribe el valor explícito.
        e.Property(x => x.ReportadoProveedor).HasConversion<int>();
        e.Property(x => x.EstadoReporteVP).HasConversion<int>();
        e.Property(x => x.VaReporteVP);
        e.Property(x => x.CategoriaReporteVP).HasMaxLength(120);   // columna heredada (texto)
        e.Property(x => x.NotaReporteVP).HasMaxLength(2000);

        // Datos estructurados de la gestión (guardados como int)
        e.Property(x => x.TipoSolucion).HasConversion<int>();
        e.Property(x => x.TipoActualizacionDatos).HasConversion<int>();
        e.Property(x => x.TicketProveedor).HasMaxLength(40);
        e.Property(x => x.AreaDestino).HasMaxLength(120);
        e.Property(x => x.CausaRaiz).HasMaxLength(1000);
        e.Property(x => x.TicketCausaRaiz).HasMaxLength(40);

        // Índices para los indicadores del dashboard (agrupar por sistema/solución)
        e.HasIndex(x => x.SistemaId);
        e.HasIndex(x => x.TipoSolucion);
        e.HasIndex(x => x.TipoActualizacionDatos);

        // El reporte se consulta SIEMPRE por "va al VP + período": índice compuesto.
        e.HasIndex(x => new { x.VaReporteVP, x.PeriodoReporteVP });
        e.HasIndex(x => x.ReportadoProveedor);

        // Sistema (eje de clasificación) y tipo de corrección: catálogos locales.
        // Restrict: un sistema con casos no se borra, se desactiva.
        e.HasOne(x => x.Sistema)
            .WithMany(si => si.Seguimientos)
            .HasForeignKey(x => x.SistemaId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.TipoCorreccion)
            .WithMany()
            .HasForeignKey(x => x.TipoCorreccionId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.CategoriaVP)
            .WithMany(c => c.Seguimientos)
            .HasForeignKey(x => x.CategoriaReporteVPId)
            .OnDelete(DeleteBehavior.SetNull);

        e.HasMany(x => x.Actualizaciones).WithOne(a => a.Seguimiento!).HasForeignKey(a => a.SeguimientoId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Jiras).WithOne(j => j.Seguimiento!).HasForeignKey(j => j.SeguimientoId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.ReportesProveedor).WithOne(r => r.Seguimiento!).HasForeignKey(r => r.SeguimientoId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Evidencias).WithOne(ev => ev.Seguimiento!).HasForeignKey(ev => ev.SeguimientoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SeguimientoActualizacionConfig : IEntityTypeConfiguration<SeguimientoActualizacion>
{
    public void Configure(EntityTypeBuilder<SeguimientoActualizacion> e)
    {
        e.ToTable("SeguimientoActualizacion");
        e.HasKey(x => x.ActualizacionId);
        e.Property(x => x.TipoActualizacion).HasMaxLength(30).IsRequired();
        e.Property(x => x.Contenido).IsRequired();
        e.Property(x => x.EtapaAnterior).HasConversion<int?>();
        e.Property(x => x.EtapaNueva).HasConversion<int?>();
        e.Property(x => x.Autor).HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.SeguimientoId);
        e.HasIndex(x => x.CreadoEn);
    }
}

public class JiraRelacionConfig : IEntityTypeConfiguration<JiraRelacion>
{
    public void Configure(EntityTypeBuilder<JiraRelacion> e)
    {
        e.ToTable("JiraRelacion");
        e.HasKey(x => x.JiraId);
        e.Property(x => x.ClaveJira).HasMaxLength(40).IsRequired();
        e.Property(x => x.Titulo).HasMaxLength(300);
        e.Property(x => x.Estado).HasConversion<int>();
        e.Property(x => x.Url).HasMaxLength(500);
        e.Property(x => x.VinculadoPor).HasMaxLength(100);
        e.HasIndex(x => x.SeguimientoId);
        e.HasIndex(x => x.ClaveJira);
    }
}

public class ProveedorConfig : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> e)
    {
        e.ToTable("Proveedor");
        e.HasKey(x => x.ProveedorId);
        e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        e.Property(x => x.Contacto).HasMaxLength(120);
        e.Property(x => x.Email).HasMaxLength(150);
        e.HasMany(x => x.Reportes).WithOne(r => r.Proveedor!).HasForeignKey(r => r.ProveedorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ReporteProveedorConfig : IEntityTypeConfiguration<ReporteProveedor>
{
    public void Configure(EntityTypeBuilder<ReporteProveedor> e)
    {
        e.ToTable("ReporteProveedor");
        e.HasKey(x => x.ReporteId);
        e.Property(x => x.NumeroCasoExterno).HasMaxLength(60);
        e.Property(x => x.RefNumSistema).HasMaxLength(20);
        e.Property(x => x.TipoVinculo).HasMaxLength(5);
        e.Property(x => x.Titulo).HasMaxLength(250);
        e.Property(x => x.Urgencia).HasMaxLength(10).HasDefaultValue("Media");
        e.Property(x => x.Estado).HasConversion<int>();
        e.Property(x => x.ResponsableInterno).HasMaxLength(120);
        e.Property(x => x.CreadoPor).HasMaxLength(100);
        e.Property(x => x.ModificadoPor).HasMaxLength(100);
        // Relación con seguimiento AHORA opcional: puede no haber ticket
        e.HasOne(x => x.Seguimiento).WithMany(s => s.ReportesProveedor)
            .HasForeignKey(x => x.SeguimientoId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => x.SeguimientoId);
        e.HasIndex(x => x.ProveedorId);
        e.HasIndex(x => x.Estado);
        e.HasIndex(x => x.FechaReporte);
        e.HasIndex(x => x.ResponsableInterno);
    }
}

public class EvidenciaConfig : IEntityTypeConfiguration<Evidencia>
{
    public void Configure(EntityTypeBuilder<Evidencia> e)
    {
        e.ToTable("Evidencia");
        e.HasKey(x => x.EvidenciaId);
        e.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
        e.Property(x => x.RutaOUrl).HasMaxLength(500);
        e.Property(x => x.TipoArchivo).HasMaxLength(50);
        e.Property(x => x.SubidoPor).HasMaxLength(100);
        e.HasIndex(x => x.SeguimientoId);
    }
}

public class CategoriaReporteVPConfig : IEntityTypeConfiguration<CategoriaReporteVP>
{
    public void Configure(EntityTypeBuilder<CategoriaReporteVP> e)
    {
        e.ToTable("CategoriaReporteVP");
        e.HasKey(x => x.CategoriaId);
        e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        e.Property(x => x.Descripcion).HasMaxLength(400);
        e.Property(x => x.Color).HasMaxLength(20);
        e.Property(x => x.Activo);   // sin HasDefaultValue: un false explícito debe guardarse como false
        e.HasIndex(x => x.Nombre).IsUnique();
        e.HasIndex(x => new { x.Activo, x.Orden });
    }
}

public class SistemaConfig : IEntityTypeConfiguration<Sistema>
{
    public void Configure(EntityTypeBuilder<Sistema> e)
    {
        e.ToTable("Sistema");
        e.HasKey(x => x.SistemaId);
        e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        e.Property(x => x.Descripcion).HasMaxLength(400);
        e.Property(x => x.Color).HasMaxLength(20);
        // Sin HasDefaultValue en Activo/Criticidad/Orden/AplicaSubtipo: un 0/false explícito debe escribirse.
        e.HasIndex(x => x.Nombre).IsUnique();
        e.HasIndex(x => x.ProveedorId);
        e.HasOne(x => x.Proveedor)
            .WithMany(p => p.Sistemas)
            .HasForeignKey(x => x.ProveedorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TipoCorreccionConfig : IEntityTypeConfiguration<TipoCorreccion>
{
    public void Configure(EntityTypeBuilder<TipoCorreccion> e)
    {
        e.ToTable("TipoCorreccion");
        e.HasKey(x => x.TipoCorreccionId);
        e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        e.Property(x => x.Descripcion).HasMaxLength(400);
        e.HasIndex(x => x.Nombre).IsUnique();
    }
}
