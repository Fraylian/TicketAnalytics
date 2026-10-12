namespace TicketAnalytics.Domain.Common;

/// <summary>
/// Conversion de fechas de CA Service Desk Manager (Clarity SM).
/// CA SDM guarda las fechas como enteros Unix epoch (segundos). El offset
/// incluye el ajuste de zona horaria de Republica Dominicana, confirmado
/// contra el SP nativo del banco (SP_TicketsServiceDesk).
/// </summary>
public static class EpochRD
{
    public static readonly DateTime BaseTicket =
        new DateTime(1969, 12, 31, 20, 8, 28, 267, DateTimeKind.Unspecified);

    public static readonly DateTime BaseActLog =
        new DateTime(1969, 12, 31, 20, 0, 0, 0, DateTimeKind.Unspecified);

    public static DateTime? ToDateTime(int? epoch)
        => (epoch is null or 0) ? null : BaseTicket.AddSeconds(epoch.Value);

    public static int ToEpoch(DateTime fecha)
        => (int)(fecha - BaseTicket).TotalSeconds;

    public static DateTime? ActLogToDateTime(int? epoch)
        => (epoch is null or 0) ? null : BaseActLog.AddSeconds(epoch.Value);

    // ===== Rangos de fecha (criterio único para toda la aplicación) =====
    //
    // Problema que resuelve: si el usuario pide 01/08 a 21/08 y comparamos
    // "fecha <= 21/08 00:00:00", se pierde todo el día 21. La regla es usar
    // SIEMPRE límite inferior inclusivo y límite superior EXCLUSIVO (+1 día).

    /// <summary>Epoch del inicio del día (00:00:00) de la fecha dada. Límite inferior inclusivo.</summary>
    public static int InicioDelDia(DateTime fecha) => ToEpoch(fecha.Date);

    /// <summary>Epoch del inicio del día SIGUIENTE. Úsalo como límite superior EXCLUSIVO
    /// (epoch &lt; este valor) para que el último día quede incluido completo.</summary>
    public static int FinDelDiaExclusivo(DateTime fecha) => ToEpoch(fecha.Date.AddDays(1));

    /// <summary>Convierte un rango [desde, hasta] de la UI al par epoch que deben usar
    /// las consultas: (desdeEpoch inclusivo, hastaEpochExclusivo). Si no hay 'hasta',
    /// devuelve null en el segundo elemento (sin tope).</summary>
    public static (int Desde, int? HastaExclusivo) Rango(DateTime? desde, DateTime? hasta, DateTime? porDefectoDesde = null)
    {
        var d = desde ?? porDefectoDesde ?? new DateTime(2026, 1, 1);
        return (InicioDelDia(d), hasta.HasValue ? FinDelDiaExclusivo(hasta.Value) : (int?)null);
    }
}
