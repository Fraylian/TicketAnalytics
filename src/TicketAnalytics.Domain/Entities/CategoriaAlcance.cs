namespace TicketAnalytics.Domain.Entities;

public class CategoriaAlcance
{
    public string Persid { get; set; } = default!;
    public string NombreCategoria { get; set; } = default!;
    public string TipoCaso { get; set; } = default!;
    public bool Activo { get; set; } = true;
}
