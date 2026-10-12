namespace TicketAnalytics.Application.Sla.DTOs;

public class SlaTicketDto
{
    public string NoTicket { get; set; } = default!;
    public string EstadoSla { get; set; } = "N/A";
    public DateTime? FechaApertura { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public DateTime? FechaCierre { get; set; }
    public double? HorasTranscurridas { get; set; }
    public double? HorasRestantes { get; set; }
    public double? PctConsumido { get; set; }
    public bool ProximoAVencer { get; set; }
    public bool Vencido { get; set; }
}

public class ResumenSlaDto
{
    public int Total { get; set; }
    public int DentroSla { get; set; }
    public int Vencidos { get; set; }
    public int ProximosAVencer { get; set; }
    public int EnCurso { get; set; }
    public decimal PctCumplimiento { get; set; }
    public double PromedioHorasResolucion { get; set; }
}
