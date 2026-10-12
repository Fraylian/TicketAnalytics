namespace TicketAnalytics.Infrastructure.Persistence.Sdm.Entities;

/// <summary>[MDB].dbo.chgcat — categoría de una orden de cambio.
/// El join en CA SDM es chg.category = chgcat.code (a diferencia de los
/// tickets, que usan prob_ctg.persid). El campo sym trae la ruta legible,
/// p. ej. "Tecnología de la Información.Orden de Cambio.Sistemas...".</summary>
public class ChgCat
{
    public string code { get; set; } = default!;   // clave; = chg.category
    public string? sym { get; set; }               // ruta legible de la categoría
    public string? persid { get; set; }
}
