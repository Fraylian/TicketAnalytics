using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Tickets.DTOs;

namespace TicketAnalytics.API.Endpoints;

public static class AnalistaEndpoints
{
    public static void MapAnalistaEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/analistas").WithTags("Analistas");

        g.MapGet("/espera-proveedor", async (string? uuid, string? nombre, IIndicadoresGestionService s, CancellationToken ct) =>
            Results.Ok(await s.EnEsperaProveedorAsync(uuid, nombre, ct)))
         .WithSummary("Casos del analista en etapa 'En espera de proveedor': ticket, sistema, proveedor y desde cuándo.");

        g.MapPost("/metricas", async (TicketFiltro f, IAnalistaService s, CancellationToken ct) => Results.Ok(await s.MetricasAsync(f, ct)))
         .WithSummary("Métricas de productividad por analista (dinámicas según rango).");

        g.MapPost("/metricas/{uuid}", async (string uuid, TicketFiltro f, IAnalistaService s, CancellationToken ct) =>
        {
            var m = await s.MetricaDeAnalistaAsync(uuid, f, ct);
            return m is null ? Results.NotFound() : Results.Ok(m);
        }).WithSummary("Métricas de un analista específico.");
    }
}
