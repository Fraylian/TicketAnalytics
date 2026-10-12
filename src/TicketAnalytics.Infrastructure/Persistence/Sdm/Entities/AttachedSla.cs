namespace TicketAnalytics.Infrastructure.Persistence.Sdm.Entities;

/// <summary>[MDB].dbo.attached_sla. Provee time_to_violation para SLA exacto.</summary>
public class AttachedSla
{
    public int id { get; set; }
    public int ticket_id { get; set; }
    public string ticket_type { get; set; } = default!;
    public int? sla_viol_status { get; set; }
    public int? time_to_violation { get; set; }
    public int del { get; set; }
}
