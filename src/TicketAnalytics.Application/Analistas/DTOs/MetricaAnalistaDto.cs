namespace TicketAnalytics.Application.Analistas.DTOs;

/// <summary>Productividad y carga operativa por analista, calculada dinamicamente
/// segun el rango de fechas. Cubre tickets y ordenes de cambio.</summary>
public class MetricaAnalistaDto
{
    public string Analista { get; set; } = default!;
    public string? AnalistaUuid { get; set; }

    // Tickets
    public int TotalRecibidos { get; set; }
    public int TotalCerrados { get; set; }
    public int TotalActivos { get; set; }
    public int DiasConApertura { get; set; }
    public int DiasConCierre { get; set; }
    public decimal PromedioRecibidosDia { get; set; }
    public decimal PromedioCerradosDia { get; set; }
    public double PromedioHorasResolucion { get; set; }
    public decimal PctCumplimientoSla { get; set; }

    // Ordenes de cambio
    public int OcRecibidas { get; set; }
    public int OcCerradas { get; set; }
    public int OcDiasApertura { get; set; }
    public int OcDiasCierre { get; set; }
    public decimal OcPromedioRecibidasDia { get; set; }
    public decimal OcPromedioCerradasDia { get; set; }

    // Seguimiento operativo (base local)
    public int CasosPendientesSeguimiento { get; set; }
    public int CasosEnEsperaProveedor { get; set; }
    public int ActualizacionesRealizadas { get; set; }
}
