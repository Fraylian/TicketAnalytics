namespace TicketAnalytics.Domain.Entities;

/// <summary>Trazabilidad: delta detectado entre snapshots. Base LOCAL.</summary>
public class TicketChangeLog
{
    public long ChangeLogId { get; set; }
    public int CrId { get; set; }
    public string RefNum { get; set; } = default!;
    public string Campo { get; set; } = default!;
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public DateTime DetectadoEn { get; set; } = DateTime.UtcNow;
}
