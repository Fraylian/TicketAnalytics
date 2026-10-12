namespace TicketAnalytics.Application.Seguimiento.DTOs;

public class SeguimientoDto
{
    public long SeguimientoId { get; set; }
    public string RefNum { get; set; } = default!;
    public string TipoOrigen { get; set; } = "cr";     // "cr" = ticket, "chg" = orden de cambio
    public int? CrId { get; set; }
    public string? Categoria { get; set; }
    public string? TipoCaso { get; set; }
    public int EtapaActual { get; set; }
    public string EtapaActualTexto { get; set; } = "";
    public string? TecnicoResponsable { get; set; }
    public string? OrdenCambioRef { get; set; }
    public bool ContinuaBajoOC { get; set; }
    public string? ProximosPasos { get; set; }
    public DateTime? ProximoSeguimiento { get; set; }
    public bool Activo { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }
    public int CantidadActualizaciones { get; set; }
    public int CantidadJiras { get; set; }
    public int CantidadReportesProveedor { get; set; }
    public int CantidadEvidencias { get; set; }

    // ---- Clasificación estructurada de la gestión ----
    public int ReportadoProveedor { get; set; }              // 0=No, 1=Si, 2=Pendiente
    public DateTime? FechaReporteProveedor { get; set; }
    public string? TicketProveedor { get; set; }
    public int Sistema { get; set; }
    public string SistemaTexto { get; set; } = "";
    public int TipoSolucion { get; set; }
    public string TipoSolucionTexto { get; set; } = "";
    public int TipoActualizacionDatos { get; set; }
    public string TipoActualizacionDatosTexto { get; set; } = "";
    public string? AreaDestino { get; set; }
    public string? CausaRaiz { get; set; }
    public string? TicketCausaRaiz { get; set; }

    // ---- Reporte VP ----
    public bool VaReporteVP { get; set; }
    public DateOnly? PeriodoReporteVP { get; set; }
    public int? CategoriaReporteVPId { get; set; }
    public int EstadoReporteVP { get; set; }
    public string? NotaReporteVP { get; set; }

    // ---- Lo que YA está registrado (para no duplicar reportes) ----
    public List<ReporteProveedorResumenDto> ReportesProveedor { get; set; } = new();
    public List<JiraResumenDto> Jiras { get; set; } = new();
}

/// <summary>Resumen del reporte a proveedor ya creado, para mostrarlo en vez de
/// dejar registrar otro igual.</summary>
public class ReporteProveedorResumenDto
{
    public long ReporteId { get; set; }
    public int ProveedorId { get; set; }
    public string Proveedor { get; set; } = "";
    public string? NumeroCasoExterno { get; set; }
    public string? Titulo { get; set; }
    public string Urgencia { get; set; } = "";
    public int Estado { get; set; }
    public string EstadoTexto { get; set; } = "";
    public DateTime FechaReporte { get; set; }
    public bool? CumplioSla { get; set; }
    public string? ResultadoSolucion { get; set; }
}

/// <summary>Resumen de un JIRA ya vinculado.</summary>
public class JiraResumenDto
{
    public long JiraId { get; set; }
    public string ClaveJira { get; set; } = "";
    public string? Titulo { get; set; }
    public int Estado { get; set; }
    public string EstadoTexto { get; set; } = "";
    public string? Url { get; set; }
    public DateTime VinculadoEn { get; set; }
}

public class ActualizacionDto
{
    public long ActualizacionId { get; set; }
    public string TipoActualizacion { get; set; } = "Avance";
    public string Contenido { get; set; } = default!;
    public int? EtapaAnterior { get; set; }
    public int? EtapaNueva { get; set; }
    public string? EtapaNuevaTexto { get; set; }
    public string Autor { get; set; } = default!;
    public DateTime CreadoEn { get; set; }
}

public class CrearSeguimientoRequest
{
    public string RefNum { get; set; } = default!;
    public int? CrId { get; set; }
    public string? Categoria { get; set; }
    public string? TipoCaso { get; set; }
    public string? TecnicoResponsable { get; set; }
    /// <summary>Si el trabajo del ticket continua bajo una OC, su chg_ref_num.</summary>
    public string? OrdenCambioRef { get; set; }
}

public class AgregarActualizacionRequest
{
    public string TipoActualizacion { get; set; } = "Avance";
    public string Contenido { get; set; } = default!;
    public int? NuevaEtapa { get; set; }           // si cambia de etapa
    public string Autor { get; set; } = default!;
    public string? ProximosPasos { get; set; }
    public DateTime? ProximoSeguimiento { get; set; }
}
