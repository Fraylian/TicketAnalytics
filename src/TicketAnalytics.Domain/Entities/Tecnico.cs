namespace TicketAnalytics.Domain.Entities;

/// <summary>Catalogo propio de tecnicos de TI. Se puede poblar desde los
/// analistas reales de mdb (por su UUID de ca_contact) o agregar manualmente.</summary>
public class Tecnico
{
    public int TecnicoId { get; set; }
    public string Nombre { get; set; } = default!;
    public byte[]? ContactUuid { get; set; }     // enlace al analista real de mdb (ca_contact)
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public List<ActividadDiaria> Actividades { get; set; } = new();
}
