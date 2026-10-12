namespace TicketAnalytics.Application.Proveedores.DTOs;

public class ProveedorDto
{
    public int ProveedorId { get; set; }
    public string Nombre { get; set; } = default!;
    public string? Contacto { get; set; }
    public string? Email { get; set; }
    public bool Activo { get; set; }
    public int ReportesAbiertos { get; set; }
}

public class ReporteProveedorDto
{
    public long ReporteId { get; set; }
    public long? SeguimientoId { get; set; }
    public string? RefNum { get; set; }
    public int ProveedorId { get; set; }
    public string? ProveedorNombre { get; set; }
    public string? NumeroCasoExterno { get; set; }
    public DateTime FechaReporte { get; set; }
    public string? ContextoEnviado { get; set; }
    public string? RespuestasRecibidas { get; set; }
    public int Estado { get; set; }
    public string EstadoTexto { get; set; } = "";
    public DateTime? UltimoSeguimiento { get; set; }
    public DateTime? ProximoSeguimiento { get; set; }
    public string? ResponsableInterno { get; set; }
    public string? ResultadoSolucion { get; set; }
    public int DiasSinRespuesta { get; set; }
    public bool NecesitaSeguimiento { get; set; }
}

public class JiraDto
{
    public long JiraId { get; set; }
    public long? SeguimientoId { get; set; }
    public string ClaveJira { get; set; } = default!;
    public string? Titulo { get; set; }
    public int Estado { get; set; }
    public string? Url { get; set; }
}
