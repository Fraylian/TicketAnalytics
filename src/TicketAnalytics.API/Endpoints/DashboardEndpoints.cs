using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Tickets.DTOs;

namespace TicketAnalytics.API.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/dashboard").WithTags("Dashboard");

        // Indicadores de la clasificación estructurada (sistema, tipo de solución,
        // actualizaciones de datos, proveedor, causa raíz, VP).
        g.MapGet("/indicadores-gestion", async (DateTime? desde, DateTime? hasta,
            IIndicadoresGestionService svc, CancellationToken ct) =>
            Results.Ok(await svc.ObtenerAsync(desde, hasta, ct)))
         .WithSummary("Tickets por sistema, tipo de solución, actualizaciones de datos, proveedor y causa raíz.");
        g.MapPost("/volumen", async (TicketFiltro f, IDashboardQueryService s, CancellationToken ct) => Results.Ok(await s.VolumenPorEstadoAsync(f, ct)));
        g.MapPost("/sla", async (TicketFiltro f, IDashboardQueryService s, CancellationToken ct) => Results.Ok(await s.CumplimientoSlaAsync(f, ct)));
        g.MapPost("/productividad", async (TicketFiltro f, IDashboardQueryService s, CancellationToken ct) => Results.Ok(await s.ProductividadAsync(f, ct)));
        g.MapPost("/tendencias/{granularidad}", async (string granularidad, TicketFiltro f, IDashboardQueryService s, CancellationToken ct) => Results.Ok(await s.TendenciasAsync(f, granularidad, ct)));
        g.MapPost("/cruce-oc", async (TicketFiltro f, IDashboardQueryService s, CancellationToken ct) => Results.Ok(await s.CruceOrdenCambioAsync(f, ct)));
    }
}
