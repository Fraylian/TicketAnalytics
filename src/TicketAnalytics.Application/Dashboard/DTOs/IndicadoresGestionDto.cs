namespace TicketAnalytics.Application.Dashboard.DTOs;

/// <summary>Un corte del tipo "nombre → cantidad" para gráficos de barras/donut.</summary>
public class ConteoClasificacionDto
{
    public int Valor { get; set; }
    public string Nombre { get; set; } = "";
    public int Cantidad { get; set; }
    public double Porcentaje { get; set; }
    /// <summary>Color fijo del elemento (sistemas); null = paleta del gráfico.</summary>
    public string? Color { get; set; }
    /// <summary>Texto secundario (p. ej. el proveedor del sistema).</summary>
    public string? Detalle { get; set; }
}

/// <summary>Un sistema con el desglose de las soluciones que se dieron a sus casos.</summary>
public class SistemaSolucionDto
{
    public int SistemaId { get; set; }
    public string Sistema { get; set; } = "";
    public string? Proveedor { get; set; }
    public string? Color { get; set; }
    public int Total { get; set; }
    /// <summary>Casos del sistema aún sin tipo de solución.</summary>
    public int SinSolucion { get; set; }
    public List<ConteoClasificacionDto> Soluciones { get; set; } = new();
}

/// <summary>Entradas contra cierres de un mes, para tickets y órdenes de cambio.</summary>
public class FlujoMensualDto
{
    public string Periodo { get; set; } = "";        // yyyy-MM
    public int TicketsEntraron { get; set; }
    public int TicketsCerrados { get; set; }
    public int OcEntraron { get; set; }
    public int OcCerradas { get; set; }
}

/// <summary>Fila del Excel de sistemas: un caso clasificado.</summary>
public class CasoClasificadoDto
{
    public string RefNum { get; set; } = "";
    public string TipoOrigen { get; set; } = "cr";
    public string? Sistema { get; set; }
    public string? Proveedor { get; set; }
    public string? TipoSolucion { get; set; }
    public string? TipoActualizacion { get; set; }
    public string? TipoCorreccion { get; set; }
    public string Etapa { get; set; } = "";
    public string? Tecnico { get; set; }
    public string? CausaRaiz { get; set; }
    public string ReportadoProveedor { get; set; } = "";
    public DateTime CreadoEn { get; set; }
}

/// <summary>Caso con reporte a proveedor pendiente o en espera, para listarlo (no solo contarlo).</summary>
public class CasoProveedorDto
{
    public long SeguimientoId { get; set; }
    public string RefNum { get; set; } = "";
    public string TipoOrigen { get; set; } = "cr";
    public string? Sistema { get; set; }
    public string? Proveedor { get; set; }
    public string? Tecnico { get; set; }
    public string Etapa { get; set; } = "";
    public string? TicketProveedor { get; set; }
    /// <summary>Desde cuándo está pendiente (inicio de la espera o creación del seguimiento).</summary>
    public DateTime Desde { get; set; }
    public int Dias { get; set; }
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
    public List<ConteoClasificacionDto> PorTipoCorreccion { get; set; } = new();
    /// <summary>Causas raíz más repetidas (texto normalizado).</summary>
    public List<ConteoClasificacionDto> PorCausaRaiz { get; set; } = new();

    /// <summary>Bloque principal del dashboard: cada sistema con las soluciones de sus casos.</summary>
    public List<SistemaSolucionDto> SistemasConSolucion { get; set; } = new();
    /// <summary>Casos gestionados que todavía no tienen sistema asignado.</summary>
    public int SinSistema { get; set; }

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
