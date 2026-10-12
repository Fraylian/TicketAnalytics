namespace TicketAnalytics.Web.Models;

/// <summary>Payload del formulario "Registrar gestión" completo (una sola llamada).</summary>
public class GestionCasoRequest
{
    public string RefNum { get; set; } = "";
    public string TipoOrigen { get; set; } = "cr";
    public string? Categoria { get; set; }
    public string? TipoCaso { get; set; }
    public string? Usuario { get; set; }

    public int? NuevaEtapa { get; set; }
    public string? TecnicoResponsable { get; set; }
    public string? ProximosPasos { get; set; }
    public DateTime? ProximoSeguimiento { get; set; }

    // Clasificación estructurada de la gestión
    public int? Sistema { get; set; }
    public int? TipoSolucion { get; set; }
    public int? TipoActualizacionDatos { get; set; }
    public string? AreaDestino { get; set; }
    public string? Comentarios { get; set; }
    public string? CausaRaiz { get; set; }
    public string? TicketCausaRaiz { get; set; }

    public AvanceGestionDto? Avance { get; set; }

    /// <summary>0 = No, 1 = Sí, 2 = Pendiente.</summary>
    public int ReportadoProveedor { get; set; } = 2;
    public DateTime? FechaReporteProveedor { get; set; }
    public string? TicketProveedor { get; set; }
    public ProveedorGestionDto? Proveedor { get; set; }
    public JiraGestionDto? Jira { get; set; }
    public ReporteVPGestionDto? ReporteVP { get; set; }
}

public class AvanceGestionDto
{
    public int Tipo { get; set; }
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
    /// <summary>Ciclo de vida del reporte creado (EstadoReporteProveedor).</summary>
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
    public string? Periodo { get; set; }
    public int? CategoriaId { get; set; }
    public int Estado { get; set; }
    public string? Nota { get; set; }
}

public class GestionCasoResultado
{
    public long SeguimientoId { get; set; }
    public string RefNum { get; set; } = "";
    public bool SeguimientoCreado { get; set; }
    public bool AvanceRegistrado { get; set; }
    public bool ProveedorRegistrado { get; set; }
    public bool JiraRegistrado { get; set; }
    public bool ReporteVPActualizado { get; set; }
    public bool ProveedorYaExistia { get; set; }
    public bool JiraYaExistia { get; set; }
    public List<string> Mensajes { get; set; } = new();
}
