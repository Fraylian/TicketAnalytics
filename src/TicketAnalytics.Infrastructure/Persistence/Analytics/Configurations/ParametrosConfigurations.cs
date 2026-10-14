using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketAnalytics.Domain.Entities;

namespace TicketAnalytics.Infrastructure.Persistence.Analytics.Configurations;

// Sin HasData: la semilla la hace AnalyticsDbInitializer con SQL idempotente
// ("inserta solo las claves que falten"), igual en base nueva y en base existente.

public class ParametroSistemaConfig : IEntityTypeConfiguration<ParametroSistema>
{
    public void Configure(EntityTypeBuilder<ParametroSistema> e)
    {
        e.ToTable("ParametroSistema");
        e.HasKey(x => x.Clave);
        e.Property(x => x.Clave).HasMaxLength(80);
        e.Property(x => x.Valor).HasMaxLength(200).IsRequired();
        e.Property(x => x.Descripcion).HasMaxLength(400);
        e.Property(x => x.ActualizadoPor).HasMaxLength(100);
    }
}

public class AnalistaGrupoConfig : IEntityTypeConfiguration<AnalistaGrupo>
{
    public void Configure(EntityTypeBuilder<AnalistaGrupo> e)
    {
        e.ToTable("AnalistaGrupo");
        e.HasKey(x => x.UserId);
        e.Property(x => x.UserId).HasMaxLength(60);
    }
}
