using TicketAnalytics.Domain.Enums;

namespace TicketAnalytics.Domain.Entities;

/// <summary>
/// Cabecera de seguimiento operativo de un ticket. Vive en la base local.
/// Se identifica por RefNum (el numero visible del ticket en mdb). El trabajo
/// continua aunque el ticket cierre en mdb, y puede continuar bajo una OC.
/// </summary>
public class TicketSeguimiento
{
    public long SeguimientoId { get; set; }

    /// <summary>Numero visible del caso en mdb: ref_num del ticket o chg_ref_num de la OC.</summary>
    public string RefNum { get; set; } = default!;

    /// <summary>Distingue si RefNum apunta a un ticket (cr) o a una orden de cambio (chg).
    /// Permite que un ticket y su OC tengan seguimiento propio sin colisionar.</summary>
    public string TipoOrigen { get; set; } = "cr";          // "cr" | "chg"

    public int? CrId { get; set; }                          // call_req.id o chg.id segun TipoOrigen
    public string? Categoria { get; set; }
    public string? TipoCaso { get; set; }

    public EtapaSeguimiento EtapaActual { get; set; } = EtapaSeguimiento.PendienteAnalisis;
    public string? TecnicoResponsable { get; set; }
    public byte[]? TecnicoUuid { get; set; }

    // Continuidad bajo orden de cambio
    public string? OrdenCambioRef { get; set; }             // chg_ref_num si el trabajo sigue bajo OC
    public bool ContinuaBajoOC { get; set; }

    public string? ProximosPasos { get; set; }
    public DateTime? ProximoSeguimiento { get; set; }

    public bool Activo { get; set; } = true;                // false cuando etapa = Cerrado/Cancelado
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    public string? CreadoPor { get; set; }

    // --- Reporte a proveedor (No / Si / Pendiente) ---
    /// <summary>Si el caso debe escalarse a un proveedor externo. Tipado con enum:
    /// evita los "Si"/"SI"/"sí" sueltos que había antes como string libre.</summary>
    public EstadoReporteProveedorTicket ReportadoProveedor { get; set; } = EstadoReporteProveedorTicket.Pendiente;

    // --- Reporte mensual al VP ---
    /// <summary>Marca que este caso debe incluirse en el reporte mensual al VP.</summary>
    public bool VaReporteVP { get; set; }

    /// <summary>Período (mes) del reporte al que pertenece. Se guarda el día 1 del mes:
    /// 2026-08-01 = Agosto 2026. Sin esto un caso marcado hoy saldría en todos los meses.</summary>
    public DateOnly? PeriodoReporteVP { get; set; }

    /// <summary>Categoría del catálogo con la que se agrupa en el reporte.</summary>
    public int? CategoriaReporteVPId { get; set; }
    public CategoriaReporteVP? CategoriaVP { get; set; }

    /// <summary>Categoría en texto (columna heredada). Se conserva para no perder los
    /// valores guardados antes del catálogo; el catálogo manda cuando hay Id.</summary>
    public string? CategoriaReporteVP { get; set; }

    /// <summary>Estado ejecutivo del caso en el reporte (Desarrollo, QA, Producción...).</summary>
    public EstadoReporteVP EstadoReporteVP { get; set; } = EstadoReporteVP.Pendiente;

    /// <summary>Nota/comentario que se muestra en la tarjeta del reporte del VP.</summary>
    public string? NotaReporteVP { get; set; }

    // ===== Datos estructurados de la gestión (puntos 5, 6, 7 del requerimiento) =====

    /// <summary>Sistema donde se presentó el caso (catálogo local). Es el eje principal de
    /// clasificación; el proveedor responsable sale del sistema.</summary>
    public int? SistemaId { get; set; }
    public Sistema? Sistema { get; set; }

    /// <summary>Tipo de corrección (catálogo local, distinto del tipo de solución).</summary>
    public int? TipoCorreccionId { get; set; }
    public TipoCorreccion? TipoCorreccion { get; set; }

    /// <summary>Clasificación de la solución aplicada. Estructurado para reportería.</summary>
    public TipoSolucion TipoSolucion { get; set; } = TipoSolucion.NoEspecificado;

    /// <summary>Subtipo de actualización de datos. Solo aplica si el sistema lo exige
    /// (Sistema.AplicaSubtipoActualizacion) y TipoSolución = Actualización de datos.</summary>
    public TipoActualizacionDatos TipoActualizacionDatos { get; set; } = TipoActualizacionDatos.NoAplica;

    /// <summary>Fecha en que el caso fue escalado al proveedor.</summary>
    public DateTime? FechaReporteProveedor { get; set; }

    /// <summary>Número de caso generado por el proveedor.</summary>
    public string? TicketProveedor { get; set; }

    /// <summary>Área responsable o involucrada en la solución.</summary>
    public string? AreaDestino { get; set; }

    /// <summary>Motivo identificado del incidente (causa raíz).</summary>
    public string? CausaRaiz { get; set; }

    /// <summary>Ticket asociado a la corrección definitiva de la causa raíz.</summary>
    public string? TicketCausaRaiz { get; set; }

    // Navegacion
    public List<SeguimientoActualizacion> Actualizaciones { get; set; } = new();
    public List<JiraRelacion> Jiras { get; set; } = new();
    public List<ReporteProveedor> ReportesProveedor { get; set; } = new();
    public List<Evidencia> Evidencias { get; set; } = new();
}
