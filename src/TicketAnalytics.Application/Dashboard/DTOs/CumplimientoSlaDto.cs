namespace TicketAnalytics.Application.Dashboard.DTOs;
public class CumplimientoSlaDto
{
    public string Categoria { get; set; } = default!;
    public int TotalTickets { get; set; }
    public int SlaCumplido { get; set; }
    public int SlaIncumplido { get; set; }
    public int SlaEnCurso { get; set; }
    public decimal PctCumplimiento { get; set; }
}
