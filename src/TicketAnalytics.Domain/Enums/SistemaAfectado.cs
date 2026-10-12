namespace TicketAnalytics.Domain.Enums;

/// <summary>
/// Sistema donde se presentó el incidente. Estructurado (no texto libre) para poder
/// medir después "tickets por sistema". Los valores 0-99 son fijos; el catálogo en
/// base permite agregar más sin recompilar, pero estos cubren lo que el área maneja hoy.
/// </summary>
public enum SistemaAfectado
{
    NoEspecificado = 0,
    Extreme = 1,
    Datawarehouse = 2,
    BscEnLinea = 3,
    Itc = 4,
    AppMovil = 5,
    Core = 6,
    Cajeros = 7,
    Otros = 99
}

public static class SistemaAfectadoInfo
{
    public static string Nombre(SistemaAfectado s) => s switch
    {
        SistemaAfectado.Extreme => "Extreme",
        SistemaAfectado.Datawarehouse => "Datawarehouse",
        SistemaAfectado.BscEnLinea => "BSC en Línea",
        SistemaAfectado.Itc => "ITC",
        SistemaAfectado.AppMovil => "App Móvil",
        SistemaAfectado.Core => "Core Bancario",
        SistemaAfectado.Cajeros => "Cajeros / ATM",
        SistemaAfectado.Otros => "Otros",
        _ => "No especificado"
    };

    public static IEnumerable<(int Valor, string Nombre)> Opciones()
        => Enum.GetValues<SistemaAfectado>()
               .Where(s => s != SistemaAfectado.NoEspecificado)
               .Select(s => ((int)s, Nombre(s)));
}
