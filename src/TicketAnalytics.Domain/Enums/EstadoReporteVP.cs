namespace TicketAnalytics.Domain.Enums;

/// <summary>
/// Estado ejecutivo con el que un caso aparece en el reporte mensual al VP.
/// Fuente única de verdad para nombre, color y orden de presentación.
/// </summary>
public enum EstadoReporteVP
{
    Pendiente = 0,
    EnDesarrollo = 1,
    EnQA = 2,
    EnProduccion = 3,
    Completado = 4,
    Bloqueado = 5
}

/// <summary>Metadatos de presentación de <see cref="EstadoReporteVP"/>.</summary>
public static class EstadoReporteVPInfo
{
    /// <summary>Orden de presentación en gráficos y agrupaciones ejecutivas.</summary>
    public static readonly EstadoReporteVP[] Orden =
    {
        EstadoReporteVP.Completado,
        EstadoReporteVP.EnProduccion,
        EstadoReporteVP.EnQA,
        EstadoReporteVP.EnDesarrollo,
        EstadoReporteVP.Pendiente,
        EstadoReporteVP.Bloqueado
    };

    public static string Nombre(EstadoReporteVP e) => e switch
    {
        EstadoReporteVP.Pendiente => "Pendiente",
        EstadoReporteVP.EnDesarrollo => "Desarrollo",
        EstadoReporteVP.EnQA => "QA",
        EstadoReporteVP.EnProduccion => "Producción",
        EstadoReporteVP.Completado => "Completado",
        EstadoReporteVP.Bloqueado => "Bloqueado",
        _ => e.ToString()
    };

    /// <summary>Color hex del estado (mismo lenguaje visual en UI, gráficos y Excel).</summary>
    public static string Color(EstadoReporteVP e) => e switch
    {
        EstadoReporteVP.Completado => "#2E7D32",
        EstadoReporteVP.EnProduccion => "#1976D2",
        EstadoReporteVP.EnQA => "#ED6C02",
        EstadoReporteVP.EnDesarrollo => "#6B4EFF",
        EstadoReporteVP.Pendiente => "#667085",
        EstadoReporteVP.Bloqueado => "#C62828",
        _ => "#667085"
    };

    /// <summary>Agrupación gruesa para KPIs: completado / proceso / pendiente.</summary>
    public static string Grupo(EstadoReporteVP e) => e switch
    {
        EstadoReporteVP.Completado => "Completado",
        EstadoReporteVP.EnProduccion or EstadoReporteVP.EnQA or EstadoReporteVP.EnDesarrollo => "En proceso",
        _ => "Pendiente"
    };

    /// <summary>True si el valor entero recibido es un estado válido (validación de request).</summary>
    public static bool EsValido(int valor) => Enum.IsDefined(typeof(EstadoReporteVP), valor);
}
