using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Seguimiento.DTOs;
using TicketAnalytics.Application.Tickets.DTOs;

namespace TicketAnalytics.API.Endpoints;

public static class SeguimientoEndpoints
{
    public static void MapSeguimientoEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/seguimiento").WithTags("Seguimiento");

        g.MapPost("/{seguimientoId:long}/finalizar", async (long seguimientoId, FinalizarGestionRequest req,
            ISeguimientoService svc, CancellationToken ct) =>
        {
            try { await svc.FinalizarAsync(seguimientoId, req.Motivo, req.Usuario, ct); return Results.NoContent(); }
            catch (KeyNotFoundException ex) { return Results.Problem(title: "No encontrado", detail: ex.Message, statusCode: 404); }
        }).WithSummary("Finaliza la gestión del caso (pasa a gestiones finalizadas).");

        g.MapPost("/{seguimientoId:long}/reabrir", async (long seguimientoId, FinalizarGestionRequest req,
            ISeguimientoService svc, CancellationToken ct) =>
        {
            try { await svc.ReabrirAsync(seguimientoId, req.Usuario, ct); return Results.NoContent(); }
            catch (KeyNotFoundException ex) { return Results.Problem(title: "No encontrado", detail: ex.Message, statusCode: 404); }
        }).WithSummary("Reabre una gestión finalizada.");

        g.MapGet("/{refNum}", async (string refNum, ISeguimientoService s, CancellationToken ct) =>
        {
            var d = await s.ObtenerPorRefNumAsync(refNum, ct);
            return d is null ? Results.NotFound() : Results.Ok(d);
        }).WithSummary("Seguimiento operativo de un ticket.");

        g.MapPost("/", async (CrearSeguimientoRequest req, ISeguimientoService s, CancellationToken ct) =>
            Results.Ok(await s.CrearAsync(req, ct)))
         .WithSummary("Crea el seguimiento operativo de un ticket.");

        g.MapPost("/{refNum}/actualizaciones", async (string refNum, AgregarActualizacionRequest req, ISeguimientoService s, CancellationToken ct) =>
            Results.Ok(await s.AgregarActualizacionAsync(refNum, req, ct)))
         .WithSummary("Agrega una actualización al historial (con posible cambio de etapa).");

        g.MapGet("/{refNum}/historial", async (string refNum, ISeguimientoService s, CancellationToken ct) =>
            Results.Ok(await s.HistorialAsync(refNum, ct)))
         .WithSummary("Historial cronológico completo del seguimiento.");

        g.MapPost("/buscar", async (TicketFiltro f, ISeguimientoService s, CancellationToken ct) =>
            Results.Ok(await s.BuscarAsync(f, ct)))
         .WithSummary("Busca seguimientos por etapa, proveedor, JIRA, sin actualización reciente, etc.");
    }
}

public record FinalizarGestionRequest(string Motivo, string? Usuario);
