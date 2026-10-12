namespace TicketAnalytics.Domain.Entities;

/// <summary>Nota/seguimiento del VP o analista. Base LOCAL, no toca MDB.</summary>
public class TicketAnotacion
{
    public long AnotacionId { get; set; }
    public string RefNum { get; set; } = default!;
    public string Autor { get; set; } = default!;
    public string Nota { get; set; } = default!;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
