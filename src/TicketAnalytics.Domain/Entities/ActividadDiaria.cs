using TicketAnalytics.Domain.Enums;

namespace TicketAnalytics.Domain.Entities;

/// <summary>Registro de trabajo diario de un tecnico. El campo NoSolicitud puede
/// enlazar con un ticket/OC real de mdb (RefNumVinculado) o ser texto libre.</summary>
public class ActividadDiaria
{
    public long ActividadId { get; set; }

    public DateOnly Fecha { get; set; }

    public int TecnicoId { get; set; }
    public Tecnico? Tecnico { get; set; }

    public string Actividad { get; set; } = default!;
    public TipoActividad Tipo { get; set; }

    // No. Solicitud: texto libre O enlace a ticket/OC real de mdb
    public string? NoSolicitud { get; set; }
    public string? RefNumVinculado { get; set; }   // ref_num de mdb si se seleccionó uno real
    public string? TipoVinculo { get; set; }        // "cr" | "chg" | null

    public string? Proyecto { get; set; }
    public decimal Horas { get; set; }
    public EstadoActividad Estado { get; set; }
    public string? Comentario { get; set; }

    // Auditoría
    public string? CreadoPor { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public string? ModificadoPor { get; set; }
    public DateTime? ModificadoEn { get; set; }
}
