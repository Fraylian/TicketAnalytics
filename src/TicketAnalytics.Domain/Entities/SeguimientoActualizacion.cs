using TicketAnalytics.Domain.Enums;

namespace TicketAnalytics.Domain.Entities;

/// <summary>
/// Entrada del historial cronologico auditable de un seguimiento. Cada avance,
/// observacion, decision, bloqueo, reunion, validacion, prueba o resultado
/// queda registrado con autor, fecha y cambio de etapa si lo hubo.
/// </summary>
public class SeguimientoActualizacion
{
    public long ActualizacionId { get; set; }
    public long SeguimientoId { get; set; }
    public TicketSeguimiento? Seguimiento { get; set; }

    public string TipoActualizacion { get; set; } = "Avance"; // Avance/Observacion/Decision/Bloqueo/Reunion/Validacion/Prueba/Resultado
    public string Contenido { get; set; } = default!;

    public EtapaSeguimiento? EtapaAnterior { get; set; }
    public EtapaSeguimiento? EtapaNueva { get; set; }

    public string Autor { get; set; } = default!;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
