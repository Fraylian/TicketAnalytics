using TicketAnalytics.Domain.Enums;

namespace TicketAnalytics.Domain.Entities;

/// <summary>Caso de JIRA relacionado a un seguimiento (uno o varios por ticket).</summary>
public class JiraRelacion
{
    public long JiraId { get; set; }
    public long SeguimientoId { get; set; }
    public TicketSeguimiento? Seguimiento { get; set; }

    public string ClaveJira { get; set; } = default!;      // ej. "BSC-1234"
    public string? Titulo { get; set; }
    public EstadoJira Estado { get; set; } = EstadoJira.PorHacer;
    public string? Url { get; set; }
    public DateTime VinculadoEn { get; set; } = DateTime.UtcNow;
    public string? VinculadoPor { get; set; }
}
