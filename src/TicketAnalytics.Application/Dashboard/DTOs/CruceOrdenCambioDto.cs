namespace TicketAnalytics.Application.Dashboard.DTOs;
public class CruceOrdenCambioDto
{
    public string Categoria { get; set; } = default!;
    public int ReqConCambio { get; set; }
    public int CambiosAunAbiertos { get; set; }
    public int CambiosCerrados { get; set; }
    public int ReqCerradoCambioPendiente { get; set; }
}
