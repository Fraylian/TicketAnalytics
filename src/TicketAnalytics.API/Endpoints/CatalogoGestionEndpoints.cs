using TicketAnalytics.Domain.Enums;

namespace TicketAnalytics.API.Endpoints;

/// <summary>Catálogos estructurados (Sistema, Tipo Solución, Tipo Actualización de Datos)
/// para poblar los selectores del formulario de gestión.</summary>
public static class CatalogoGestionEndpoints
{
    public static void MapCatalogoGestionEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/catalogos-gestion").WithTags("Catálogos de gestión");

        g.MapGet("/sistemas", () =>
            Results.Ok(SistemaAfectadoInfo.Opciones().Select(o => new { valor = o.Valor, nombre = o.Nombre })))
         .WithSummary("Sistemas afectados (estructurado).");

        g.MapGet("/tipos-solucion", () =>
            Results.Ok(TipoSolucionInfo.Opciones().Select(o => new { valor = o.Valor, nombre = o.Nombre })))
         .WithSummary("Tipos de solución (estructurado).");

        g.MapGet("/tipos-actualizacion-datos", () =>
            Results.Ok(TipoActualizacionDatosInfo.Opciones().Select(o => new { valor = o.Valor, nombre = o.Nombre })))
         .WithSummary("Subtipos de actualización de datos (solo BSC en Línea + Actualización de datos).");

        // Todo en una sola llamada, para cachear del lado del cliente.
        g.MapGet("/todos", () => Results.Ok(new
        {
            sistemas = SistemaAfectadoInfo.Opciones().Select(o => new { valor = o.Valor, nombre = o.Nombre }),
            tiposSolucion = TipoSolucionInfo.Opciones().Select(o => new { valor = o.Valor, nombre = o.Nombre }),
            tiposActualizacion = TipoActualizacionDatosInfo.Opciones().Select(o => new { valor = o.Valor, nombre = o.Nombre })
        })).WithSummary("Todos los catálogos de gestión en una sola respuesta.");
    }
}
