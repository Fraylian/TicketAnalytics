using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Tickets.DTOs;

namespace TicketAnalytics.API.Endpoints;

/// <summary>
/// Endpoints de ordenes de cambio UNIFICADAS con sus tickets (desde el mismo
/// contexto de MDB), reemplazando el antiguo flujo de Excel separados.
/// </summary>
public static class OrdenCambioEndpoints
{
    public static void MapOrdenCambioEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/ordenes-cambio").WithTags("Ordenes de Cambio");

        g.MapGet("/{chgRefNum}", async (string chgRefNum, ITicketQueryService svc, CancellationToken ct) =>
        {
            var oc = await svc.ObtenerOrdenCambioConTicketsAsync(chgRefNum, ct);
            return oc is null ? Results.NotFound() : Results.Ok(oc);
        }).WithSummary("Una OC con TODOS sus tickets relacionados, en una sola consulta.");

        g.MapPost("/buscar", async (TicketFiltro filtro, ITicketQueryService svc, CancellationToken ct) =>
            Results.Ok(await svc.BuscarOrdenesCambioAsync(filtro, ct)))
         .WithSummary("Lista OCs del alcance con sus tickets, aplicando filtros.");

        g.MapPost("/tablero", async (TicketFiltro filtro, ITicketQueryService svc, CancellationToken ct) =>
            Results.Ok(await svc.TableroOrdenesCambioAsync(filtro, ct)))
         .WithSummary("Tablero de OC: apartados por vencimiento (vencidas, vencen este mes, por vencer, cerradas) y resumen por técnico.");

        // ===== Módulo avanzado: dashboard, vencimientos, filtros por analista =====
        g.MapPost("/buscar-avanzado", async (OrdenCambioFiltro f, IOrdenCambioDashboardService svc, CancellationToken ct) =>
            Results.Ok(await svc.BuscarAsync(f, ct)))
         .WithSummary("Búsqueda avanzada con filtros de vencimiento, analista y mes/año.");

        g.MapPost("/dashboard", async (OrdenCambioFiltro f, IOrdenCambioDashboardService svc, CancellationToken ct) =>
            Results.Ok(await svc.DashboardAsync(f, ct)))
         .WithSummary("KPIs y datos de gráficos del dashboard de OC.");

        g.MapPost("/control-vencimientos", async (OrdenCambioFiltro f, IOrdenCambioDashboardService svc, CancellationToken ct) =>
            Results.Ok(await svc.ControlVencimientosAsync(f, ct)))
         .WithSummary("Sección Control de vencimientos: KPIs, distribución y 'Vencen primero'.");

        g.MapPost("/analistas", async (OrdenCambioFiltro f, IOrdenCambioDashboardService svc, CancellationToken ct) =>
            Results.Ok(await svc.AnalistasConOcAsync(f, ct)))
         .WithSummary("Analistas con OC en el alcance, para el selector de filtro.");
    }
}
