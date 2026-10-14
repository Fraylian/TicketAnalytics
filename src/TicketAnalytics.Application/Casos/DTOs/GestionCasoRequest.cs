namespace TicketAnalytics.Application.Casos.DTOs;

/// <summary>
/// Registra en UNA sola operación todo lo que el formulario "Registrar gestión"
/// puede guardar. Antes eran 4 llamadas sueltas (seguimiento, proveedor, JIRA,
/// avance) y si fallaba una intermedia quedaba la gestión a medias.
/// Todas las escrituras son sobre la base local Analytics, así que van en una
/// transacción única.
/// </summary>
public class GestionCasoRequest
{
    public string RefNum { get; set; } = "";
    public string TipoOrigen { get; set; } = "cr";      // cr | chg
    public string? Categoria { get; set; }
    public string? TipoCaso { get; set; }
    public string? Usuario { get; set; }

    // --- Seguimiento / avance ---
    public int? NuevaEtapa { get; set; }                // EtapaSeguimiento
    public string? TecnicoResponsable { get; set; }
    public string? ProximosPasos { get; set; }
    public DateTime? ProximoSeguimiento { get; set; }

    // --- Datos estructurados de la gestión (puntos 5, 6, 7) ---
    public int? Sistema { get; set; }                   // SistemaId del catálogo local (0 = quitar)
    public int? TipoCorreccionId { get; set; }          // catálogo TipoCorreccion (0 = quitar)
    public int? TipoSolucion { get; set; }              // TipoSolucion
    public int? TipoActualizacionDatos { get; set; }    // TipoActualizacionDatos (solo si el sistema lo exige + Actualización de datos)
    public string? AreaDestino { get; set; }
    public string? Comentarios { get; set; }            // qué se hizo (va como avance)
    public string? CausaRaiz { get; set; }
    public string? TicketCausaRaiz { get; set; }

    public AvanceGestionDto? Avance { get; set; }

    // --- Proveedor ---
    /// <summary>0 = No, 1 = Sí, 2 = Pendiente (EstadoReporteProveedorTicket).</summary>
    public int ReportadoProveedor { get; set; } = 2;
    public DateTime? FechaReporteProveedor { get; set; }
    public string? TicketProveedor { get; set; }
    public ProveedorGestionDto? Proveedor { get; set; }

    // --- JIRA ---
    public JiraGestionDto? Jira { get; set; }

    // --- Reporte VP ---
    public ReporteVPGestionDto? ReporteVP { get; set; }
}

public class AvanceGestionDto
{
    public int Tipo { get; set; }                       // TipoActividad / tipo de avance
    public string? Detalle { get; set; }
    public decimal? Horas { get; set; }
}

public class ProveedorGestionDto
{
    public int ProveedorId { get; set; }
    public string? NumeroCasoExterno { get; set; }
    public string? Titulo { get; set; }
    public string? Descripcion { get; set; }
    public string? ContextoEnviado { get; set; }
    public string? RespuestasRecibidas { get; set; }
    public string? ResultadoSolucion { get; set; }
    public string? Urgencia { get; set; }
    /// <summary>Ciclo de vida del reporte ya creado (EstadoReporteProveedor). NO confundir
    /// con ReportadoProveedor (que solo dice si va al proveedor).</summary>
    public int Estado { get; set; }
    public bool? CumplioSla { get; set; }
    public DateTime? FechaReporte { get; set; }
    public string? ResponsableInterno { get; set; }
}

public class JiraGestionDto
{
    public string ClaveJira { get; set; } = "";
    public string? Titulo { get; set; }
    public int? Estado { get; set; }
    public string? Url { get; set; }
}

public class ReporteVPGestionDto
{
    public bool Incluir { get; set; }
    public string? Periodo { get; set; }                // yyyy-MM
    public int? CategoriaId { get; set; }
    public int Estado { get; set; }                     // EstadoReporteVP
    public string? Nota { get; set; }
}

/// <summary>Qué se guardó realmente, para que la UI informe con precisión.</summary>
public class GestionCasoResultado
{
    public long SeguimientoId { get; set; }
    public string RefNum { get; set; } = "";
    public bool SeguimientoCreado { get; set; }
    public bool AvanceRegistrado { get; set; }
    public bool ProveedorRegistrado { get; set; }
    public bool JiraRegistrado { get; set; }
    public bool ReporteVPActualizado { get; set; }
    /// <summary>True si no se creó el reporte porque el caso ya tenía uno.</summary>
    public bool ProveedorYaExistia { get; set; }
    /// <summary>True si el JIRA ya estaba vinculado.</summary>
    public bool JiraYaExistia { get; set; }
    public List<string> Mensajes { get; set; } = new();
}
