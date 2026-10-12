using TicketAnalytics.Domain.Enums;

namespace TicketAnalytics.Domain.Entities;

/// <summary>Complejidad de OC gestionada por la app (CA SDM no la captura). Base LOCAL.</summary>
public class OrdenCambioComplejidad
{
    public string ChgRefNum { get; set; } = default!;
    public NivelComplejidad Complejidad { get; set; }
    public string? AsignadoPor { get; set; }
    public DateTime AsignadoEn { get; set; } = DateTime.UtcNow;
}
