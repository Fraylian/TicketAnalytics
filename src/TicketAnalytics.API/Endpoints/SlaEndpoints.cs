using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Tickets.DTOs;

namespace TicketAnalytics.API.Endpoints;

public static class SlaEndpoints
{
    public static void MapSlaEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sla").WithTags("SLA");

        g.MapPost("/resumen", async (TicketFiltro f, ISlaService s, CancellationToken ct) => Results.Ok(await s.ResumenAsync(f, ct)))
         .WithSummary("Resumen de SLA: totales, % cumplimiento, vencidos, próximos a vencer.");

        g.MapPost("/proximos-vencer", async (TicketFiltro f, ISlaService s, CancellationToken ct) => Results.Ok(await s.TicketsProximosAVencerAsync(f, ct)))
         .WithSummary("Tickets próximos a vencer (80%+ de SLA consumido).");

        g.MapPost("/vencidos", async (TicketFiltro f, ISlaService s, CancellationToken ct) => Results.Ok(await s.TicketsVencidosAsync(f, ct)))
         .WithSummary("Tickets con SLA vencido.");

        g.MapGet("/{refNum}", async (string refNum, ISlaService s, CancellationToken ct) =>
        {
            var r = await s.SlaDeTicketAsync(refNum, ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        }).WithSummary("SLA con tiempos de un ticket específico.");
    }
}
