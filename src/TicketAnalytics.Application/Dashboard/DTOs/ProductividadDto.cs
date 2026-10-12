namespace TicketAnalytics.Application.Dashboard.DTOs;
public class ProductividadDto
{
    public string Grupo { get; set; } = default!;
    public string Analista { get; set; } = default!;
    public int TotalAsignados { get; set; }
    public int Resueltos { get; set; }
    public int ConSlaViolado { get; set; }
    public int GeneraronCambio { get; set; }
    public decimal PromedioHorasResolucion { get; set; }
}
