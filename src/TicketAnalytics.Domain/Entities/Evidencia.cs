namespace TicketAnalytics.Domain.Entities;

/// <summary>Evidencia o documento asociado a un seguimiento (metadatos; el binario
/// puede vivir en un fileshare o storage; aqui se guarda la referencia).</summary>
public class Evidencia
{
    public long EvidenciaId { get; set; }
    public long SeguimientoId { get; set; }
    public TicketSeguimiento? Seguimiento { get; set; }

    public string Nombre { get; set; } = default!;
    public string? Descripcion { get; set; }
    public string? RutaOUrl { get; set; }                  // ruta en fileshare / URL
    public string? TipoArchivo { get; set; }
    public long? TamanoBytes { get; set; }
    public DateTime SubidoEn { get; set; } = DateTime.UtcNow;
    public string? SubidoPor { get; set; }
}
