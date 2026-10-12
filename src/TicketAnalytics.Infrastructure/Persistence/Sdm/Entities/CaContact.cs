namespace TicketAnalytics.Infrastructure.Persistence.Sdm.Entities;
public class CaContact
{
    public byte[] contact_uuid { get; set; } = default!;
    public string? last_name { get; set; }
    public string? first_name { get; set; }
    public string? middle_name { get; set; }
    public string? userid { get; set; }
}
