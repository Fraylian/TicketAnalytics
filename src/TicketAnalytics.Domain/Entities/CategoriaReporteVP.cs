namespace TicketAnalytics.Domain.Entities;

/// <summary>
/// Catálogo de categorías del reporte mensual al VP (Canales Digitales, Core, etc.).
/// Existe para no repetir strings a mano en cada formulario y para poder ordenar
/// las categorías de forma definida en las presentaciones ejecutivas.
/// </summary>
public class CategoriaReporteVP
{
    public int CategoriaId { get; set; }
    public string Nombre { get; set; } = default!;
    public string? Descripcion { get; set; }
    /// <summary>Orden de presentación en el reporte (menor primero).</summary>
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    /// <summary>Color hex para gráficos y chips. Si es null se asigna de la paleta.</summary>
    public string? Color { get; set; }

    public List<TicketSeguimiento> Seguimientos { get; set; } = new();
}
