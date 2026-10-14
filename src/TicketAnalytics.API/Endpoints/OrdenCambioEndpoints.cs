using Microsoft.EntityFrameworkCore;
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

        g.MapGet("/atencion-por-persona", async (int? anio, int? mes, IOrdenCambioDashboardService svc, CancellationToken ct) =>
        {
            var hoy = DateTime.Today;
            var a = anio ?? hoy.Year;
            var m = mes ?? hoy.Month;
            if (m is < 1 or > 12 || a is < 2000 or > 2100)
                return Results.Problem(title: "Datos inválidos", detail: "Mes (1-12) o año fuera de rango.", statusCode: 400);
            return Results.Ok(await svc.AtencionPorPersonaAsync(a, m, ct));
        }).WithSummary("Por persona del grupo: OC vencidas, que vencen hoy, esta semana y en el mes; tickets pendientes de gestión. Agregado en SQL.");

        g.MapPut("/{chgRefNum}/complejidad", async (string chgRefNum, AsignarComplejidadRequest req, IComplejidadService svc,
                                                    TicketAnalytics.Infrastructure.Persistence.Sdm.SdmReadContext sdm, CancellationToken ct) =>
        {
            // IsDefined: TryParse también acepta números ("7") que no son un nivel válido.
            if (!Enum.TryParse<TicketAnalytics.Domain.Enums.NivelComplejidad>(req.Complejidad, true, out var nivel)
                || !Enum.IsDefined(nivel) || int.TryParse(req.Complejidad, out _))
                return Results.Problem(title: "Datos inválidos", detail: "Complejidad debe ser Alta, Media, Baja o SinAsignar.", statusCode: 400);
            // La OC debe existir en MDB (lectura por chg_x3, índice único de chg_ref_num).
            if (!await sdm.Changes.AsNoTracking().AnyAsync(c => c.chg_ref_num == chgRefNum, ct))
                return Results.Problem(title: "No encontrada", detail: $"No existe la orden de cambio {chgRefNum}.", statusCode: 404);
            await svc.AsignarAsync(chgRefNum, nivel, req.Usuario, ct);
            return Results.NoContent();
        }).WithSummary("Asigna la complejidad de una OC (define su vencimiento). Se guarda en la base local.");

        g.MapPost("/analistas", async (OrdenCambioFiltro f, IOrdenCambioDashboardService svc, CancellationToken ct) =>
            Results.Ok(await svc.AnalistasConOcAsync(f, ct)))
         .WithSummary("Analistas con OC en el alcance, para el selector de filtro.");
    }
}

public record AsignarComplejidadRequest(string Complejidad, string? Usuario);
