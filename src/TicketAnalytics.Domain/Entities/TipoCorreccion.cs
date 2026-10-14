namespace TicketAnalytics.Domain.Entities;

/// <summary>
/// Catálogo administrable de "tipo de corrección": campo propio de la gestión, distinto
/// del tipo de solución. Vive en la base local; los valores los define el área desde la
/// administración (no se siembran valores inventados).
/// </summary>
public class TipoCorreccion
{
    public int TipoCorreccionId { get; set; }
    public string Nombre { get; set; } = default!;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public int Orden { get; set; }
}
