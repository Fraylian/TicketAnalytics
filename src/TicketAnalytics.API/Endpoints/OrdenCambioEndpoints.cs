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

        g.MapPost("/excel", async (OrdenCambioFiltro f, IOrdenCambioDashboardService svc, ILoggerFactory lf, CancellationToken ct) =>
        {
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            var ordenes = await svc.BuscarTodasAsync(f, ct);
            var contexto = Services.ContextoExport.Oc(f);
            var bytes = Services.ListadosExcel.OrdenesCambio(ordenes, contexto);
            lf.CreateLogger("Exportaciones").LogInformation(
                "Excel de OC: {Filas} filas, {Bytes} bytes en {Ms} ms", ordenes.Count, bytes.Length, reloj.ElapsedMilliseconds);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Ordenes_Cambio_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }).WithSummary("Excel (.xlsx) con TODAS las órdenes de cambio que cumplen el filtro.");

        g.MapPost("/analistas", async (OrdenCambioFiltro f, IOrdenCambioDashboardService svc, CancellationToken ct) =>
            Results.Ok(await svc.AnalistasConOcAsync(f, ct)))
         .WithSummary("Analistas con OC en el alcance, para el selector de filtro.");
    }
}
