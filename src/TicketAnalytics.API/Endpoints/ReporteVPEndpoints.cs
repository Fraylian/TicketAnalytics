using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.ReporteVP.DTOs;
using TicketAnalytics.Domain.Enums;

namespace TicketAnalytics.API.Endpoints;

public static class ReporteVPEndpoints
{
    public static void MapReporteVPEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reporte-vp").WithTags("Reporte VP");

        g.MapPost("/overview", async (ReporteVPFiltro filtro, IReporteVPService svc, CancellationToken ct) =>
            Results.Ok(await svc.OverviewAsync(filtro, ct)))
         .WithSummary("Todo el reporte del período: KPIs, categorías, estados, tendencia y casos.");

        g.MapPost("/casos", async (ReporteVPFiltro filtro, IReporteVPService svc, CancellationToken ct) =>
            Results.Ok(await svc.CasosAsync(filtro, ct)))
         .WithSummary("Casos del período aplicando filtros, sin paginar.");

        g.MapGet("/categorias", async (IReporteVPService svc, CancellationToken ct) =>
            Results.Ok(await svc.CategoriasAsync(true, ct)))
         .WithSummary("Catálogo de categorías del reporte.");

        g.MapGet("/periodos", async (IReporteVPService svc, CancellationToken ct) =>
            Results.Ok(await svc.PeriodosDisponiblesAsync(ct)))
         .WithSummary("Períodos con casos registrados.");

        g.MapPost("/casos/guardar", async (GuardarCasoVPRequest req, IReporteVPService svc, CancellationToken ct) =>
        {
            // Validación en backend: nunca confiamos solo en el formulario.
            if (string.IsNullOrWhiteSpace(req.RefNum))
                return Results.Problem(title: "Datos incompletos", detail: "El número de caso es obligatorio.", statusCode: 400);
            if (req.RefNum.Length > 20)
                return Results.Problem(title: "Datos inválidos", detail: "El número de caso excede 20 caracteres.", statusCode: 400);
            if (req.TipoOrigen is not ("cr" or "chg"))
                return Results.Problem(title: "Datos inválidos", detail: "El tipo de origen debe ser 'cr' o 'chg'.", statusCode: 400);
            if (!EstadoReporteVPInfo.EsValido(req.Estado))
                return Results.Problem(title: "Datos inválidos", detail: $"Estado de reporte VP no válido: {req.Estado}.", statusCode: 400);
            if (!string.IsNullOrWhiteSpace(req.Periodo) && !DateOnly.TryParse($"{req.Periodo}-01", out _))
                return Results.Problem(title: "Datos inválidos", detail: "El período debe tener formato yyyy-MM.", statusCode: 400);

            try { return Results.Ok(await svc.GuardarCasoAsync(req, ct)); }
            catch (ArgumentException ex) { return Results.Problem(title: "Datos inválidos", detail: ex.Message, statusCode: 400); }
        }).WithSummary("Agrega o actualiza un caso del reporte VP.");

        g.MapDelete("/casos/{seguimientoId:long}", async (long seguimientoId, IReporteVPService svc, CancellationToken ct) =>
        {
            try { await svc.QuitarCasoAsync(seguimientoId, ct); return Results.NoContent(); }
            catch (KeyNotFoundException ex) { return Results.Problem(title: "No encontrado", detail: ex.Message, statusCode: 404); }
        }).WithSummary("Saca el caso del reporte (conserva el seguimiento).");

        g.MapPost("/casos/{seguimientoId:long}/mover", async (long seguimientoId, MoverPeriodoRequest req, IReporteVPService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Periodo) || !DateOnly.TryParse($"{req.Periodo}-01", out _))
                return Results.Problem(title: "Datos inválidos", detail: "El período debe tener formato yyyy-MM.", statusCode: 400);
            try { await svc.MoverPeriodoAsync(seguimientoId, req.Periodo, ct); return Results.NoContent(); }
            catch (KeyNotFoundException ex) { return Results.Problem(title: "No encontrado", detail: ex.Message, statusCode: 404); }
        }).WithSummary("Mueve el caso a otro período.");

        g.MapPost("/excel", async (ReporteVPFiltro filtro, IReporteVPService svc, ILoggerFactory lf, CancellationToken ct) =>
        {
            var log = lf.CreateLogger("ReporteVPExcel");
            var overview = await svc.OverviewAsync(filtro, ct);
            var bytes = Services.ReporteVPExcel.Generar(overview);
            log.LogInformation("Excel VP generado: {Periodo}, {Casos} casos, {Bytes} bytes",
                overview.Periodo, overview.TotalCasos, bytes.Length);

            var nombre = $"Reporte_VP_{overview.Periodo}.xlsx";
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
        }).WithSummary("Excel del reporte VP (Resumen Ejecutivo + Detalle) respetando los filtros.");
    }
}

public record MoverPeriodoRequest(string Periodo);
