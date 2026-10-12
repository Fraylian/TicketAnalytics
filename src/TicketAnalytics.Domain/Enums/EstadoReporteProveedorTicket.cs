namespace TicketAnalytics.Domain.Enums;

/// <summary>
/// Indica si un caso (ticket/OC) debe escalarse a un proveedor externo.
/// OJO: es un concepto DISTINTO de <see cref="EstadoReporteProveedor"/>, que
/// representa el ciclo de vida de un reporte YA creado (Abierto, EnEspera, etc.).
/// Aquí solo respondemos: ¿este caso va al proveedor?
/// </summary>
public enum EstadoReporteProveedorTicket
{
    /// <summary>Resuelto internamente; no requiere proveedor.</summary>
    No = 0,
    /// <summary>Sí se reportó (o se reportará) al proveedor.</summary>
    Si = 1,
    /// <summary>Todavía por decidir/escalar. Aparece en la bandeja de seguimiento.</summary>
    Pendiente = 2
}
