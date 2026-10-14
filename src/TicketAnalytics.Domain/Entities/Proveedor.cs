namespace TicketAnalytics.Domain.Entities;

/// <summary>Catálogo de proveedores con su SLA de respuesta por severidad.</summary>
public class Proveedor
{
    public int ProveedorId { get; set; }
    public string Nombre { get; set; } = default!;
    public string? Contacto { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public bool Activo { get; set; } = true;

    // SLA de respuesta en HORAS por severidad. Null = el proveedor no maneja ese nivel.
    // Los días del contrato se guardan ya convertidos a horas (2D = 48, 15D = 360…).
    public int? SlaCriticoHoras { get; set; }
    public int? SlaAltoHoras { get; set; }
    public int? SlaMedioHoras { get; set; }
    public int? SlaBajoHoras { get; set; }

    public List<ReporteProveedor> Reportes { get; set; } = new();

    /// <summary>Sistemas de los que este proveedor es responsable (N:1 desde Sistema).</summary>
    public List<Sistema> Sistemas { get; set; } = new();
}
