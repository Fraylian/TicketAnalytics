namespace TicketAnalytics.Application.Tickets.DTOs;

/// <summary>Conjunto completo de filtros combinables. Los null no se aplican.
/// Las fechas se convierten a epoch RD internamente (filtro sargable).</summary>
public class TicketFiltro
{
    /// <summary>Sin valor: el servidor aplica la fecha base configurada (General.FechaBase).</summary>
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string? TipoCaso { get; set; }
    public List<string>? Categorias { get; set; }
    public List<string>? Estados { get; set; }
    public string? AsignatarioUuid { get; set; }
    public string? AsignatarioNombre { get; set; }
    public string? GrupoUuid { get; set; }
    public string? Texto { get; set; }
    public bool? ConOrdenCambio { get; set; }
    public bool? SoloReqCerradoCambioPendiente { get; set; }
    public string? EstadoSla { get; set; }
    public string? ComplejidadOC { get; set; }

    // ---- Filtros de seguimiento operativo (base local) ----
    public int? EtapaSeguimiento { get; set; }             // etapa operativa
    public bool? ConJira { get; set; }
    public int? ProveedorId { get; set; }
    public bool? EnEsperaProveedor { get; set; }
    public bool? SlaVencido { get; set; }
    public bool? ProximoAVencer { get; set; }
    public bool? SinActualizacionReciente { get; set; }    // sin update en N dias
    public int? DiasSinActualizacion { get; set; } = 7;
    public bool? CerradoConSeguimientoPendiente { get; set; }
    public bool? Reabierto { get; set; }

    // Paginacion / orden

    // ---- Clasificación estructurada de la gestión (puntos 3, 6, 7) ----
    /// <summary>Estado del reporte a proveedor del caso: 0=No, 1=Si, 2=Pendiente.</summary>
    public int? ReportadoProveedor { get; set; }
    /// <summary>Atajo: solo casos con reporte a proveedor en No o Pendiente (requieren escalamiento).</summary>
    public bool? PendienteEscalarProveedor { get; set; }
    public int? Sistema { get; set; }                      // SistemaId del catálogo local
    public int? TipoSolucion { get; set; }                 // TipoSolucion
    public int? TipoActualizacionDatos { get; set; }       // TipoActualizacionDatos
    public bool? ConCausaRaiz { get; set; }
    public bool? ReportadoVP { get; set; }
    /// <summary>Solo casos abiertos / pendientes de gestión (vista por defecto de Tickets).</summary>
    public bool? SoloAbiertos { get; set; }

    /// <summary>true = solo gestiones activas, false = solo finalizadas, null = todas.</summary>
    public bool? SoloActivos { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string OrderBy { get; set; } = "FechaApertura";
    public bool OrderDesc { get; set; } = false;
}
