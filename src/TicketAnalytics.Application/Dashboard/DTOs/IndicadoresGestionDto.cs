namespace TicketAnalytics.Application.Dashboard.DTOs;

/// <summary>Un corte del tipo "nombre → cantidad" para gráficos de barras/donut.</summary>
public class ConteoClasificacionDto
{
    public int Valor { get; set; }
    public string Nombre { get; set; } = "";
    public int Cantidad { get; set; }
    public double Porcentaje { get; set; }
}

/// <summary>
/// Indicadores derivados de la clasificación estructurada de la gestión
/// (puntos 6, 7 y 8 del requerimiento): por sistema, tipo de solución,
/// tipo de actualización de datos, proveedor, causa raíz y VP.
/// </summary>
public class IndicadoresGestionDto
{
    public int TotalGestionados { get; set; }

    public List<ConteoClasificacionDto> PorSistema { get; set; } = new();
    public List<ConteoClasificacionDto> PorTipoSolucion { get; set; } = new();

    /// <summary>Desglose del caso especial: BSC en Línea + Actualización de datos.</summary>
    public List<ConteoClasificacionDto> PorTipoActualizacionDatos { get; set; } = new();
    public int TotalActualizacionesDatos { get; set; }

    public int ReportadosProveedor { get; set; }
    public int PendientesReportarProveedor { get; set; }
    public int NoRequierenProveedor { get; set; }

    public int ConCausaRaiz { get; set; }
    public int SinCausaRaiz { get; set; }
    public int ConJira { get; set; }
    public int ReportadosVP { get; set; }

    /// <summary>Sistemas con más incidencias, ya ordenado.</summary>
    public List<ConteoClasificacionDto> TopSistemas { get; set; } = new();
    /// <summary>Tipos de solución más utilizados, ya ordenado.</summary>
    public List<ConteoClasificacionDto> TopTiposSolucion { get; set; } = new();
}
