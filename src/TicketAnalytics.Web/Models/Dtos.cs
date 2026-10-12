namespace TicketAnalytics.Web.Models;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class TicketEnriquecidoDto
{
    public string TipoCaso { get; set; } = "";
    public string NoTicket { get; set; } = "";
    public string? Descripcion { get; set; }
    public string? Categoria { get; set; }
    public string? Estado { get; set; }
    public string? UsuarioFinalAfectado { get; set; }
    public string? Asignatario { get; set; }
    public string? Grupo { get; set; }
    public DateTime? FechaApertura { get; set; }
    public DateTime? FechaCierreSolicitado { get; set; }
    public string ResueltoDentroSLA { get; set; } = "N/A";
    public bool SlaViolationFlag { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public int? DiasParaVencer { get; set; }
    public string EstadoVencimiento { get; set; } = "sin_fecha";
    public string? NoOC { get; set; }
    public string? EstadoOC { get; set; }
    public string? AsignatarioOC { get; set; }
    public DateTime? FechaAperturaOC { get; set; }
    public DateTime? FechaCierreSolicitadoOC { get; set; }
    public string? ComplejidadOC { get; set; }
    public string? OCResueltoDentroSLA { get; set; }
    public bool TieneOrdenCambio { get; set; }
    public bool ReqCerradoConCambioPendiente { get; set; }
}

public class OrdenCambioConTicketsDto
{
    public int ChgId { get; set; }
    public string NoOC { get; set; } = "";
    public string? ResumenOC { get; set; }
    public string? DescripcionOC { get; set; }
    public string? EstadoOC { get; set; }
    public string? CategoriaOC { get; set; }
    public string? AsignatarioOC { get; set; }
    public string? GrupoOC { get; set; }
    public DateTime? FechaAperturaOC { get; set; }
    public DateTime? FechaCierreOC { get; set; }
    public DateTime? FechaVencimientoOC { get; set; }
    public string? ComplejidadOC { get; set; }
    public string? OCResueltoDentroSLA { get; set; }
    public int? DiasRestantes { get; set; }
    public double? PctTiempoConsumido { get; set; }
    public string EstadoVencimiento { get; set; } = "sin_fecha";
    public bool SinAnalista { get; set; }
    public bool TieneTicketCerradoOcPendiente { get; set; }
    public DateTime? UltimaGestion { get; set; }
    public int? DiasDesdeUltimaGestion { get; set; }
    public List<TicketRelacionadoDto> Tickets { get; set; } = new();
    public int CantidadTickets { get; set; }
}

public class TicketRelacionadoDto
{
    public string TipoCaso { get; set; } = "";
    public string NoTicket { get; set; } = "";
    public string? Categoria { get; set; }
    public string? Estado { get; set; }
    public string? Asignatario { get; set; }
    public string? UsuarioFinalAfectado { get; set; }
    public DateTime? FechaApertura { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string ResueltoDentroSLA { get; set; } = "N/A";
    public bool CerradoConCambioPendiente { get; set; }
}

public class VolumenPorEstadoDto
{
    public string Categoria { get; set; } = "";
    public string Estado { get; set; } = "";
    public int Total { get; set; }
    public int Abiertos { get; set; }
    public int Cerrados { get; set; }
}

public class CumplimientoSlaDto
{
    public string Categoria { get; set; } = "";
    public int TotalTickets { get; set; }
    public int SlaCumplido { get; set; }
    public int SlaIncumplido { get; set; }
    public int SlaEnCurso { get; set; }
    public decimal PctCumplimiento { get; set; }
}

public class ProductividadDto
{
    public string Grupo { get; set; } = "";
    public string Analista { get; set; } = "";
    public int TotalAsignados { get; set; }
    public int Resueltos { get; set; }
    public int ConSlaViolado { get; set; }
    public int GeneraronCambio { get; set; }
    public decimal PromedioHorasResolucion { get; set; }
}

public class TendenciaDto
{
    public string Periodo { get; set; } = "";
    public string Granularidad { get; set; } = "";
    public string TipoCaso { get; set; } = "";
    public int Abiertos { get; set; }
    public int Cerrados { get; set; }
}

public class CruceOrdenCambioDto
{
    public string Categoria { get; set; } = "";
    public int ReqConCambio { get; set; }
    public int CambiosAunAbiertos { get; set; }
    public int CambiosCerrados { get; set; }
    public int ReqCerradoCambioPendiente { get; set; }
}

// ===== SLA =====
public class SlaTicketDto
{
    public string NoTicket { get; set; } = "";
    public string EstadoSla { get; set; } = "N/A";
    public DateTime? FechaApertura { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public DateTime? FechaCierre { get; set; }
    public double? HorasTranscurridas { get; set; }
    public double? HorasRestantes { get; set; }
    public double? PctConsumido { get; set; }
    public bool ProximoAVencer { get; set; }
    public bool Vencido { get; set; }
}

public class ResumenSlaDto
{
    public int Total { get; set; }
    public int DentroSla { get; set; }
    public int Vencidos { get; set; }
    public int ProximosAVencer { get; set; }
    public int EnCurso { get; set; }
    public decimal PctCumplimiento { get; set; }
    public double PromedioHorasResolucion { get; set; }
}

// ===== Metricas por analista =====
public class MetricaAnalistaDto
{
    public string Analista { get; set; } = "";
    public string? AnalistaUuid { get; set; }
    public int TotalRecibidos { get; set; }
    public int TotalCerrados { get; set; }
    public int TotalActivos { get; set; }
    public int DiasConApertura { get; set; }
    public int DiasConCierre { get; set; }
    public decimal PromedioRecibidosDia { get; set; }
    public decimal PromedioCerradosDia { get; set; }
    public double PromedioHorasResolucion { get; set; }
    public decimal PctCumplimientoSla { get; set; }
    public int OcRecibidas { get; set; }
    public int OcCerradas { get; set; }
    public int OcDiasApertura { get; set; }
    public int OcDiasCierre { get; set; }
    public decimal OcPromedioRecibidasDia { get; set; }
    public decimal OcPromedioCerradasDia { get; set; }
    public int CasosPendientesSeguimiento { get; set; }
    public int CasosEnEsperaProveedor { get; set; }
    public int ActualizacionesRealizadas { get; set; }
}

// ===== Seguimiento =====
public class SeguimientoDto
{
    public long SeguimientoId { get; set; }
    public string RefNum { get; set; } = "";
    public string TipoOrigen { get; set; } = "cr";
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

    // Clasificación estructurada de la gestión
    public int ReportadoProveedor { get; set; }
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

    // Reporte VP
    public bool VaReporteVP { get; set; }
    public DateOnly? PeriodoReporteVP { get; set; }
    public int? CategoriaReporteVPId { get; set; }
    public int EstadoReporteVP { get; set; }
    public string? NotaReporteVP { get; set; }

    // Lo que ya está registrado (para no duplicar)
    public List<ReporteProveedorResumenDto> ReportesProveedor { get; set; } = new();
    public List<JiraResumenDto> Jiras { get; set; } = new();
}

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
    public string Contenido { get; set; } = "";
    public int? EtapaAnterior { get; set; }
    public int? EtapaNueva { get; set; }
    public string? EtapaNuevaTexto { get; set; }
    public string Autor { get; set; } = "";
    public DateTime CreadoEn { get; set; }
}

public class CrearSeguimientoRequest
{
    public string RefNum { get; set; } = "";
    public int? CrId { get; set; }
    public string? Categoria { get; set; }
    public string? TipoCaso { get; set; }
    public string? TecnicoResponsable { get; set; }
    public string? OrdenCambioRef { get; set; }
}

public class AgregarActualizacionRequest
{
    public string TipoActualizacion { get; set; } = "Avance";
    public string Contenido { get; set; } = "";
    public int? NuevaEtapa { get; set; }
    public string Autor { get; set; } = "";
    public string? ProximosPasos { get; set; }
    public DateTime? ProximoSeguimiento { get; set; }
}

// ===== Proveedores / JIRA =====
public class ProveedorDto
{
    public int ProveedorId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Contacto { get; set; }
    public string? Email { get; set; }
    public bool Activo { get; set; }
    public int ReportesAbiertos { get; set; }
}

public class ReporteProveedorDto
{
    public long ReporteId { get; set; }
    public long SeguimientoId { get; set; }
    public string? RefNum { get; set; }
    public int ProveedorId { get; set; }
    public string? ProveedorNombre { get; set; }
    public string? NumeroCasoExterno { get; set; }
    public DateTime FechaReporte { get; set; } = DateTime.Now;
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
    public long SeguimientoId { get; set; }
    public string ClaveJira { get; set; } = "";
    public string? Titulo { get; set; }
    public int Estado { get; set; }
    public string? Url { get; set; }
}

public static class EtapasSeguimiento
{
    public static readonly (int Id, string Texto)[] Todas = new[]
    {
        (0, "Pendiente de análisis"), (1, "En análisis"), (2, "En desarrollo"),
        (3, "En espera del proveedor"), (4, "Reportado al proveedor"), (5, "En espera del usuario"),
        (6, "En pruebas internas"), (7, "En QA"), (8, "Pendiente de pase"),
        (9, "Pendiente de orden de cambio"), (10, "En implementación"), (11, "En producción"),
        (12, "Cerrado"), (13, "Cancelado"), (14, "Reabierto")
    };
    public static string Texto(int id) => Todas.FirstOrDefault(e => e.Id == id).Texto ?? id.ToString();
}

/// <summary>Conteos de la bandeja de seguimiento para los filtros aplicados (todas las páginas).</summary>
public class ResumenSeguimientoDto
{
    public int Total { get; set; }
    public int ConJira { get; set; }
    public int ConProveedor { get; set; }
    public int RequierenAtencion { get; set; }
    public int SinActualizar { get; set; }
    public int EnReporteVP { get; set; }
}
