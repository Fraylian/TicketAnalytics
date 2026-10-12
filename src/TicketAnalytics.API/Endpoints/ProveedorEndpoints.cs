using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Domain.Enums;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Application.Proveedores.DTOs;

namespace TicketAnalytics.API.Endpoints;

public record CrearProveedorRequest(string Nombre, string? Contacto, string? Email);
public record VincularJiraRequest(string ClaveJira, string? Titulo, string? Url, string? Autor);

public static class ProveedorEndpoints
{
    public static void MapProveedorEndpoints(this IEndpointRouteBuilder app)
    {
        var p = app.MapGroup("/api/proveedores").WithTags("Proveedores");

        p.MapGet("/", async (IProveedorService s, CancellationToken ct) => Results.Ok(await s.ListarAsync(ct)))
         .WithSummary("Lista de proveedores con reportes abiertos.");

        p.MapPost("/", async (CrearProveedorRequest req, IProveedorService s, CancellationToken ct) =>
            Results.Ok(await s.CrearAsync(req.Nombre, req.Contacto, req.Email, ct)))
         .WithSummary("Crea un proveedor.");

        p.MapPost("/reportar/{refNum}", async (string refNum, ReporteProveedorDto req, IProveedorService s, CancellationToken ct) =>
            Results.Ok(await s.ReportarAsync(refNum, req, ct)))
         .WithSummary("Reporta un ticket a un proveedor.");

        p.MapGet("/reportes/{refNum}", async (string refNum, IProveedorService s, CancellationToken ct) =>
            Results.Ok(await s.ReportesDeSeguimientoAsync(refNum, ct)))
         .WithSummary("Reportes a proveedor de un ticket.");

        p.MapGet("/necesitan-seguimiento", async (IProveedorService s, CancellationToken ct) =>
            Results.Ok(await s.ReportesQueNecesitanSeguimientoAsync(ct)))
         .WithSummary("Reportes que llevan tiempo sin respuesta o necesitan seguimiento.");

        // ===== Matriz mensual de proveedores =====
        // Casos marcados como "No" o "Pendiente" de reportar al proveedor: bandeja de seguimiento.
        p.MapGet("/pendientes-escalar", async (AnalyticsContext db, CancellationToken ct) =>
        {
            var datos = await db.TicketSeguimientos.AsNoTracking()
                  .Where(s => s.Activo && s.ReportadoProveedor != EstadoReporteProveedorTicket.Si)
                  .OrderByDescending(s => s.ActualizadoEn)
                  .Select(s => new
                  {
                      s.SeguimientoId,
                      s.RefNum,
                      s.TipoOrigen,
                      s.Categoria,
                      s.TecnicoResponsable,
                      Estado = (int)s.ReportadoProveedor,
                      EstadoTexto = s.ReportadoProveedor == EstadoReporteProveedorTicket.Pendiente
                                    ? "Pendiente" : "No",
                      s.ProximosPasos,
                      s.ActualizadoEn
                  })
                  .ToListAsync(ct);
            return Results.Ok(datos);
        }).WithSummary("Casos con reporte a proveedor en No o Pendiente, para darles seguimiento.");

        p.MapPost("/matriz/buscar", async (ReporteProveedorFiltro f, IProveedorService s, CancellationToken ct) =>
            Results.Ok(await s.MatrizBuscarAsync(f, ct)))
         .WithSummary("Reportes de la matriz filtrados por mes calendario, proveedor y responsable.");

        // Excel real de la matriz (2 hojas: Matriz Control + DETALLES)
        p.MapPost("/matriz/excel", async (ReporteProveedorFiltro f, IProveedorService s, CancellationToken ct) =>
        {
            var reportes = await s.MatrizBuscarAsync(f, ct);
            var provs = await s.CatalogoListarAsync(ct);
            var prov = f.ProveedorId.HasValue ? provs.FirstOrDefault(x => x.ProveedorId == f.ProveedorId) : null;
            var nombreProv = prov?.Nombre ?? reportes.FirstOrDefault()?.Proveedor ?? "Todos";
            int mes = f.Mes ?? DateTime.Today.Month;
            int anio = f.Anio ?? DateTime.Today.Year;

            var bytes = TicketAnalytics.API.Services.MatrizProveedorExcel.Generar(
                nombreProv, mes, anio, null, f.ResponsableInterno, reportes, prov);

            var nombre = $"Matriz_Cumplimiento_{nombreProv}_{mes:00}_{anio}.xlsx".Replace(' ', '_');
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
        }).WithSummary("Genera y descarga el Excel de la matriz de cumplimiento (2 hojas).");

        p.MapPost("/matriz/resumen", async (ReporteProveedorFiltro f, IProveedorService s, CancellationToken ct) =>
            Results.Ok(await s.MatrizResumenAsync(f, ct)))
         .WithSummary("KPIs de la matriz mensual, incluyendo cumplimiento de SLA.");

        p.MapPost("/matriz/guardar", async (GuardarReporteProveedorRequest req, IProveedorService s, CancellationToken ct) =>
            Results.Ok(await s.MatrizGuardarAsync(req, ct)))
         .WithSummary("Crea o edita un reporte de proveedor (con o sin ticket).");

        p.MapDelete("/matriz/{reporteId:long}", async (long reporteId, IProveedorService s, CancellationToken ct) =>
        {
            await s.MatrizEliminarAsync(reporteId, ct);
            return Results.NoContent();
        }).WithSummary("Elimina un reporte de proveedor.");

        p.MapGet("/matriz/responsables", async (IProveedorService s, CancellationToken ct) =>
            Results.Ok(await s.MatrizResponsablesAsync(ct)))
         .WithSummary("Responsables que han registrado reportes (para el filtro).");

        // Grupo de analistas del área (para limitar selectores de responsable)
        // Analistas del grupo resueltos desde la base (userid -> nombre + uuid).
        // Fallback a los nombres estáticos si la base no responde.
        p.MapGet("/grupo-analistas", async (ITicketQueryService svc, CancellationToken ct) =>
        {
            try
            {
                var lista = await svc.AnalistasDelGrupoAsync(ct);
                if (lista.Count > 0) return Results.Ok(lista.Select(a => a.Nombre).ToList());
            }
            catch { }
            return Results.Ok(TicketAnalytics.Domain.Common.GrupoAnalistas.Miembros.ToList());
        }).WithSummary("Analistas del grupo/área (nombres) para selectores de responsable.");

        // Analistas del grupo con detalle (userid + uuid), para filtros por asignatario.
        p.MapGet("/grupo-analistas/detalle", async (ITicketQueryService svc, CancellationToken ct) =>
            Results.Ok(await svc.AnalistasDelGrupoAsync(ct)))
         .WithSummary("Analistas del grupo con userid y uuid.");

        p.MapGet("/matriz/por-ticket/{refNum}", async (string refNum, IProveedorService s, CancellationToken ct) =>
            Results.Ok(await s.MatrizPorRefNumSistemaAsync(refNum, ct)))
         .WithSummary("Reportes de proveedor ligados a un ticket/OC del sistema.");

        // ===== CRUD de catálogo de proveedores (con SLA y contacto) =====
        p.MapGet("/catalogo", async (IProveedorService s, CancellationToken ct) =>
            Results.Ok(await s.CatalogoListarAsync(ct)))
         .WithSummary("Catálogo de proveedores con SLA por severidad y datos de contacto.");

        p.MapPost("/catalogo/guardar", async (GuardarProveedorRequest req, IProveedorService s, CancellationToken ct) =>
            Results.Ok(await s.CatalogoGuardarAsync(req, ct)))
         .WithSummary("Crea o edita un proveedor del catálogo.");

        p.MapDelete("/catalogo/{proveedorId:int}", async (int proveedorId, IProveedorService s, CancellationToken ct) =>
        {
            await s.CatalogoEliminarAsync(proveedorId, ct);
            return Results.NoContent();
        }).WithSummary("Elimina (o desactiva si tiene historial) un proveedor.");

        var j = app.MapGroup("/api/jira").WithTags("JIRA");

        j.MapPost("/vincular/{refNum}", async (string refNum, VincularJiraRequest req, IJiraService s, CancellationToken ct) =>
            Results.Ok(await s.VincularAsync(refNum, req.ClaveJira, req.Titulo, req.Url, req.Autor, ct)))
         .WithSummary("Vincula un caso de JIRA a un ticket.");

        j.MapGet("/{refNum}", async (string refNum, IJiraService s, CancellationToken ct) =>
            Results.Ok(await s.DeSeguimientoAsync(refNum, ct)))
         .WithSummary("Casos de JIRA de un ticket.");

        j.MapDelete("/{jiraId:long}", async (long jiraId, IJiraService s, CancellationToken ct) =>
        {
            await s.DesvincularAsync(jiraId, ct);
            return Results.NoContent();
        }).WithSummary("Desvincula un caso de JIRA.");
    }
}
