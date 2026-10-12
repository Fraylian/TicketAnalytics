namespace TicketAnalytics.Infrastructure.Persistence.Sdm.Entities;
public class ProbCtg
{
    public int id { get; set; }
    public string persid { get; set; } = default!;
    public string? sym { get; set; }
    public string? service_type { get; set; }
}
