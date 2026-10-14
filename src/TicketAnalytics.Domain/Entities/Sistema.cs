namespace TicketAnalytics.Domain.Entities;

/// <summary>
/// Catálogo de sistemas (aplicaciones) donde se presentan los casos. Es el eje principal
/// de clasificación de la gestión: primero se dice QUÉ sistema falló y el proveedor sale
/// de aquí, porque es quien responde por ese sistema (relación N:1 con Proveedor).
/// Vive en la base local y se administra desde la pantalla de Proveedores | Sistemas,
/// así que agregar un sistema no requiere recompilar.
/// </summary>
public class Sistema
{
    public int SistemaId { get; set; }
    public string Nombre { get; set; } = default!;
    public string? Descripcion { get; set; }

    /// <summary>Proveedor responsable del sistema. Null = por asignar o interno.</summary>
    public int? ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }

    /// <summary>Prioridad en el dashboard: 0 sin definir, 1 baja, 2 media, 3 alta.</summary>
    public int Criticidad { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>Orden de presentación (menor primero).</summary>
    public int Orden { get; set; }

    /// <summary>Color hex fijo del sistema en todos los gráficos.</summary>
    public string? Color { get; set; }

    /// <summary>Si en este sistema la solución "Actualización de datos" exige subtipo
    /// (token enganchado, desagrupación de empresa…). Hoy solo BSC en Línea.</summary>
    public bool AplicaSubtipoActualizacion { get; set; }

    public List<TicketSeguimiento> Seguimientos { get; set; } = new();
}
