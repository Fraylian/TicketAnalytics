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

        // Bloque 3: carga de trabajo. Por mes, tickets y OC que entraron y que se cerraron.
        g.MapPost("/flujo-mensual", async (TicketFiltro f, IDashboardQueryService s, CancellationToken ct) =>
            Results.Ok(await s.FlujoMensualAsync(f, ct)))
         .WithSummary("Entradas contra cierres por mes, para tickets y órdenes de cambio (GROUP BY en SQL).");

        // Bloque 5: casos pendientes de reportar al proveedor, con el listado (paginado).
        g.MapGet("/proveedor-pendientes", async (DateTime? desde, DateTime? hasta, int? page, int? pageSize,
            IIndicadoresGestionService svc, CancellationToken ct) =>
            Results.Ok(await svc.PendientesReportarAsync(desde, hasta, page ?? 1, pageSize ?? 10, ct)))
         .WithSummary("Casos marcados 'Pendiente' de reportar al proveedor: cuáles son, con sistema y proveedor.");

        // Bloque 1 exportable: matriz sistema × solución + casos clasificados, en Excel real.
        g.MapGet("/sistemas/excel", async (DateTime? desde, DateTime? hasta, int? sistemaId,
            IIndicadoresGestionService svc, ILoggerFactory lf, CancellationToken ct) =>
        {
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            var ind = await svc.ObtenerAsync(desde, hasta, ct);
            if (sistemaId is > 0)
            {
                ind.SistemasConSolucion = ind.SistemasConSolucion.Where(x => x.SistemaId == sistemaId).ToList();
                ind.SinSistema = 0;
            }
            var casos = await svc.CasosClasificadosAsync(desde, hasta, sistemaId, ct);
            var bytes = Services.SistemasExcel.Generar(ind, casos, Services.ContextoExport.Rango(desde, hasta));
            lf.CreateLogger("Exportaciones").LogInformation(
                "Excel de sistemas: {Casos} casos, {Bytes} bytes en {Ms} ms", casos.Count, bytes.Length, reloj.ElapsedMilliseconds);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Casos_por_sistema_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }).WithSummary("Excel (.xlsx) con los casos por sistema y la solución que se les dio.");
    }
}
