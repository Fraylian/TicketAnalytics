namespace TicketAnalytics.Application.Dashboard.DTOs;
public class TendenciaDto
{
    public string Periodo { get; set; } = default!;
    public string Granularidad { get; set; } = default!;
    public string TipoCaso { get; set; } = default!;
    public int Abiertos { get; set; }
    public int Cerrados { get; set; }
}
