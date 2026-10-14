namespace TicketAnalytics.Domain.Enums;

/// <summary>
/// Subtipo que SOLO aplica cuando el sistema lo exige (BSC en Línea) y TipoSolución =
/// Actualización de datos. Estructurado porque el área quiere medir cuántos casos hay de cada tipo
/// (token enganchado, desagrupación de empresa, etc.).
/// </summary>
public enum TipoActualizacionDatos
{
    NoAplica = 0,
    DesagrupacionEmpresa = 1,
    ActualizacionUsuario = 2,
    RegistroEnganchado = 3,
    ActualizacionIdentificacion = 4,
    TokenEnganchado = 5,
    Otros = 99
}

public static class TipoActualizacionDatosInfo
{
    public static string Nombre(TipoActualizacionDatos t) => t switch
    {
        TipoActualizacionDatos.DesagrupacionEmpresa => "Desagrupación de empresa",
        TipoActualizacionDatos.ActualizacionUsuario => "Actualización de usuario",
        TipoActualizacionDatos.RegistroEnganchado => "Registro enganchado",
        TipoActualizacionDatos.ActualizacionIdentificacion => "Actualización de identificación",
        TipoActualizacionDatos.TokenEnganchado => "Token enganchado",
        TipoActualizacionDatos.Otros => "Otros",
        _ => "No aplica"
    };

    public static IEnumerable<(int Valor, string Nombre)> Opciones()
        => Enum.GetValues<TipoActualizacionDatos>()
               .Where(t => t != TipoActualizacionDatos.NoAplica)
               .Select(t => ((int)t, Nombre(t)));

    /// <summary>El subtipo solo aplica cuando el sistema lo exige (hoy BSC en Línea, marcado
    /// en el catálogo con AplicaSubtipoActualizacion) y la solución es Actualización de datos.</summary>
    public static bool Aplica(bool sistemaAplicaSubtipo, TipoSolucion solucion)
        => sistemaAplicaSubtipo && solucion == TipoSolucion.ActualizacionDatos;
}
