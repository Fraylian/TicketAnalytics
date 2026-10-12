using TicketAnalytics.Domain.Enums;

namespace TicketAnalytics.Domain.Entities;

/// <summary>Reporte/caso escalado a un proveedor. Puede estar ligado a un
/// seguimiento (ticket) o ser independiente (SeguimientoId null) cuando no hay
/// ticket en el sistema. Alimenta la matriz mensual de proveedores.</summary>
public class ReporteProveedor
{
    public long ReporteId { get; set; }

    // Opcional: si el reporte vino de un ticket/OC, queda enlazado; si no, null.
    public long? SeguimientoId { get; set; }
    public TicketSeguimiento? Seguimiento { get; set; }

    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }

    // Enlace suelto al caso de nuestro sistema (ref_num de ticket u OC), texto libre.
    public string? RefNumSistema { get; set; }
    public string? TipoVinculo { get; set; }               // "cr" | "chg" | null

    public string? NumeroCasoExterno { get; set; }         // ticket/caso del proveedor
    public string? Titulo { get; set; }                    // título del caso (matriz)
    public string? Descripcion { get; set; }               // descripción del caso (matriz)

    public DateTime FechaReporte { get; set; } = DateTime.UtcNow;
    public string? ContextoEnviado { get; set; }
    public string? RespuestasRecibidas { get; set; }

    /// <summary>Severidad del caso: Critico | Alto | Medio | Bajo. Determina qué SLA del proveedor aplica.</summary>
    public string Urgencia { get; set; } = "Medio";        // Critico | Alto | Medio | Bajo
    /// <summary>Horas de SLA que el proveedor tiene para responder según la severidad (referencia del contrato).</summary>
    public int? SlaHorasRespuesta { get; set; }
    /// <summary>Momento en que el proveedor dio su primera respuesta (para medir SLA).</summary>
    public DateTime? FechaPrimeraRespuesta { get; set; }
    /// <summary>El usuario marca si el proveedor cumplió el SLA. NULL = sin evaluar.</summary>
    public bool? CumplioSla { get; set; }
    /// <summary>Si el proveedor brindó asistencia / trabajó el caso.</summary>
    public bool? BrindoAsistencia { get; set; }
    /// <summary>Si entregó un cambio/entregable.</summary>
    public bool? EntregoCambio { get; set; }

    public EstadoReporteProveedor Estado { get; set; } = EstadoReporteProveedor.Abierto;
    public DateTime? UltimoSeguimiento { get; set; }
    public DateTime? ProximoSeguimiento { get; set; }
    public string? ResponsableInterno { get; set; }        // asignatario que reportó
    public string? ResultadoSolucion { get; set; }         // agenda / dónde está la solución

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public string? CreadoPor { get; set; }
    public DateTime? ModificadoEn { get; set; }
    public string? ModificadoPor { get; set; }
}
