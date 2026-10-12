namespace TicketAnalytics.Domain.Enums;

/// <summary>
/// Clasificación de la solución aplicada. Estructurada para medir "tipos de solución
/// más utilizados". Valores tomados del requerimiento del área.
/// </summary>
public enum TipoSolucion
{
    NoEspecificado = 0,
    ActualizacionDatos = 1,
    ErrorUsuario = 2,
    InstalacionAmbiente = 3,
    Solucionado = 4,
    EliminacionRegistro = 5,
    ConsultaUsuario = 6,
    CorreccionSistema = 7,
    Configuracion = 8,
    Otros = 99
}

public static class TipoSolucionInfo
{
    public static string Nombre(TipoSolucion t) => t switch
    {
        TipoSolucion.ActualizacionDatos => "Actualización de datos",
        TipoSolucion.ErrorUsuario => "Error de usuario",
        TipoSolucion.InstalacionAmbiente => "Instalación en ambiente",
        TipoSolucion.Solucionado => "Solucionado",
        TipoSolucion.EliminacionRegistro => "Eliminación de registro",
        TipoSolucion.ConsultaUsuario => "Consulta por parte del usuario",
        TipoSolucion.CorreccionSistema => "Corrección del sistema",
        TipoSolucion.Configuracion => "Configuración",
        TipoSolucion.Otros => "Otros",
        _ => "No especificado"
    };

    public static IEnumerable<(int Valor, string Nombre)> Opciones()
        => Enum.GetValues<TipoSolucion>()
               .Where(t => t != TipoSolucion.NoEspecificado)
               .Select(t => ((int)t, Nombre(t)));
}
