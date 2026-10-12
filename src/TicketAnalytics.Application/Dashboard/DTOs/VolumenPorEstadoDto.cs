namespace TicketAnalytics.Application.Dashboard.DTOs;
public class VolumenPorEstadoDto
{
    public string Categoria { get; set; } = default!;
    public string Estado { get; set; } = default!;
    public int Total { get; set; }
    public int Abiertos { get; set; }
    public int Cerrados { get; set; }
}
