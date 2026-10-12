namespace TicketAnalytics.Application.Tickets.DTOs;

/// <summary>Ticket con su orden de cambio asociada y SLA (reporte ejecutivo).</summary>
public class TicketEnriquecidoDto
{
    public string TipoCaso { get; set; } = default!;
    public string NoTicket { get; set; } = default!;
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
    /// <summary>Incumplimiento previsto: fecha de vencimiento del SLA (de attached_sla).</summary>
    public DateTime? FechaVencimiento { get; set; }
    /// <summary>Días restantes hasta el vencimiento (negativo = ya vencido).</summary>
    public int? DiasParaVencer { get; set; }
    /// <summary>vencido | vence_hoy | vence_pronto | en_curso | sin_fecha | cerrado</summary>
    public string EstadoVencimiento { get; set; } = "sin_fecha";

    public string? NoOC { get; set; }
    public string? EstadoOC { get; set; }
    public string? AsignatarioOC { get; set; }
    public DateTime? FechaAperturaOC { get; set; }
    public DateTime? FechaCierreSolicitadoOC { get; set; }
    public string? ComplejidadOC { get; set; }
    public string? OCResueltoDentroSLA { get; set; }

    public bool TieneOrdenCambio => !string.IsNullOrEmpty(NoOC);
    public bool ReqCerradoConCambioPendiente { get; set; }
}
