using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Tickets.DTOs;

namespace TicketAnalytics.API.Endpoints;

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/tickets").WithTags("Tickets");

        g.MapPost("/buscar", async (TicketFiltro filtro, ITicketQueryService svc, CancellationToken ct) =>
            Results.Ok(await svc.BuscarAsync(filtro, ct)))
         .WithSummary("Busca tickets enriquecidos con todos los filtros.");

        g.MapPost("/bandeja-analistas", async (TicketFiltro filtro, ITicketQueryService svc, CancellationToken ct) =>
            Results.Ok(await svc.BandejaTicketsPorAnalistaAsync(filtro, ct)))
         .WithSummary("Bandeja de tickets abiertos por analista del grupo, con alertas de vencimiento.");

        g.MapGet("/{refNum}", async (string refNum, ITicketQueryService svc, CancellationToken ct) =>
        {
            var t = await svc.ObtenerPorRefNumAsync(refNum, ct);
            return t is null ? Results.NotFound() : Results.Ok(t);
        }).WithSummary("Detalle de un ticket con su OC y SLA.");
    }
}
