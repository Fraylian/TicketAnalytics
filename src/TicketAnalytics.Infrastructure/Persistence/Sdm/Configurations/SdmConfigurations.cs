using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketAnalytics.Infrastructure.Persistence.Sdm.Entities;

namespace TicketAnalytics.Infrastructure.Persistence.Sdm.Configurations;

public class CallReqConfig : IEntityTypeConfiguration<CallReq>
{
    public void Configure(EntityTypeBuilder<CallReq> e)
    {
        e.ToTable("call_req", "dbo");
        e.HasKey(x => x.id);
        e.Property(x => x.id).ValueGeneratedNever();
    }
}
public class ChgConfig : IEntityTypeConfiguration<Chg>
{
    public void Configure(EntityTypeBuilder<Chg> e)
    {
        e.ToTable("chg", "dbo");
        e.HasKey(x => x.id);
        e.Property(x => x.id).ValueGeneratedNever();
    }
}
public class AttachedSlaConfig : IEntityTypeConfiguration<AttachedSla>
{
    public void Configure(EntityTypeBuilder<AttachedSla> e)
    {
        e.ToTable("attached_sla", "dbo");
        e.HasKey(x => x.id);
        e.Property(x => x.id).ValueGeneratedNever();
    }
}
public class ProbCtgConfig : IEntityTypeConfiguration<ProbCtg>
{
    public void Configure(EntityTypeBuilder<ProbCtg> e)
    {
        e.ToTable("prob_ctg", "dbo");
        e.HasKey(x => x.id);
        e.Property(x => x.id).ValueGeneratedNever();
    }
}
public class CrStatConfig : IEntityTypeConfiguration<CrStat>
{
    public void Configure(EntityTypeBuilder<CrStat> e)
    {
        e.ToTable("cr_stat", "dbo");
        e.HasKey(x => x.code);
    }
}
public class ChgStatConfig : IEntityTypeConfiguration<ChgStat>
{
    public void Configure(EntityTypeBuilder<ChgStat> e)
    {
        e.ToTable("chgstat", "dbo");
        e.HasKey(x => x.code);
    }
}
public class ChgCatConfig : IEntityTypeConfiguration<ChgCat>
{
    public void Configure(EntityTypeBuilder<ChgCat> e)
    {
        e.ToTable("chgcat", "dbo");
        e.HasKey(x => x.code);
    }
}
public class CaContactConfig : IEntityTypeConfiguration<CaContact>
{
    public void Configure(EntityTypeBuilder<CaContact> e)
    {
        e.ToTable("ca_contact", "dbo");
        e.HasKey(x => x.contact_uuid);
        e.Property(x => x.contact_uuid).HasColumnType("binary(16)");
    }
}
public class GrpMemConfig : IEntityTypeConfiguration<GrpMem>
{
    public void Configure(EntityTypeBuilder<GrpMem> e)
    {
        e.ToTable("grpmem", "dbo");
        e.HasNoKey();
        e.Property(x => x.group_id).HasColumnType("binary(16)");
        e.Property(x => x.member).HasColumnType("binary(16)");
    }
}
