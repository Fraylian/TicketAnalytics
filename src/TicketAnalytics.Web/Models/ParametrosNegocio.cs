namespace TicketAnalytics.Web.Models;

/// <summary>
/// Parámetros de negocio vigentes (GET api/parametros). Se configuran en la tabla local
/// ParametroSistema; los valores por defecto aquí solo cubren el caso de que la API no responda.
/// </summary>
public class ParametrosNegocioDto
{
    public DateTime FechaBase { get; set; } = new(2026, 1, 1);
    public int OcDiasAlta { get; set; } = 90;
    public int OcDiasMedia { get; set; } = 60;
    public int OcDiasBaja { get; set; } = 30;
    public int OcDiasSinAsignar { get; set; } = 60;
    public int OcDiasVencePronto { get; set; } = 7;
    public int TicketDiasVencePronto { get; set; } = 2;
    public int TicketPctRiesgoSla { get; set; } = 80;
    public int AlertasMinutosAntelacion { get; set; } = 60;
    public int AlertasMinutosAvisoTemprano { get; set; } = 240;
    public int SeguimientoDiasSinAvance { get; set; } = 7;
}
