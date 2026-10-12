using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.TrabajoDiario.DTOs;

namespace TicketAnalytics.API.Endpoints;

public static class TrabajoDiarioEndpoints
{
    public static void MapTrabajoDiarioEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/trabajo-diario").WithTags("Trabajo diario");

        // Técnicos
        g.MapGet("/tecnicos", async (ITrabajoDiarioService s, CancellationToken ct) => Results.Ok(await s.ListarTecnicosAsync(ct)))
         .WithSummary("Lista de técnicos activos.");
        g.MapPost("/tecnicos/sincronizar", async (ITrabajoDiarioService s, CancellationToken ct) =>
            Results.Ok(new { agregados = await s.SincronizarTecnicosDesdeMdbAsync(ct) }))
         .WithSummary("Llena el catálogo de técnicos desde los analistas reales de mdb.");
        g.MapPost("/tecnicos", async (CrearTecnicoReq req, ITrabajoDiarioService s, CancellationToken ct) =>
            Results.Ok(await s.CrearTecnicoAsync(req.Nombre, ct)))
         .WithSummary("Agrega un técnico manualmente.");

        // Actividades
        g.MapPost("/buscar", async (ActividadFiltro f, ITrabajoDiarioService s, CancellationToken ct) =>
            Results.Ok(await s.BuscarAsync(f, ct)))
         .WithSummary("Tabla de actividades con filtros, orden y paginación.");
        g.MapPost("/", async (GuardarActividadRequest req, ITrabajoDiarioService s, CancellationToken ct) =>
            Results.Ok(await s.GuardarAsync(req, ct)))
         .WithSummary("Crea o edita una actividad.");
        g.MapDelete("/{id:long}", async (long id, string? usuario, ITrabajoDiarioService s, CancellationToken ct) =>
            await s.EliminarAsync(id, usuario, ct) ? Results.NoContent() : Results.NotFound())
         .WithSummary("Elimina una actividad.");

        // Resumen (KPIs + gráficos)
        g.MapPost("/resumen", async (ActividadFiltro f, ITrabajoDiarioService s, CancellationToken ct) =>
            Results.Ok(await s.ResumenAsync(f, ct)))
         .WithSummary("KPIs, carga por técnico y datos de los gráficos.");

        // Enlace con mdb
        g.MapGet("/buscar-tickets", async (string termino, ITrabajoDiarioService s, CancellationToken ct) =>
            Results.Ok(await s.BuscarTicketsAsync(termino, ct)))
         .WithSummary("Autocompletar No. Solicitud con tickets/OC reales de mdb.");
        g.MapGet("/ticket/{refNum}/actividades", async (string refNum, ITrabajoDiarioService s, CancellationToken ct) =>
            Results.Ok(await s.ActividadesDeTicketAsync(refNum, ct)))
         .WithSummary("Desglose: cuánto se ha trabajado en un ticket/OC.");
    }
}

public record CrearTecnicoReq(string Nombre);
