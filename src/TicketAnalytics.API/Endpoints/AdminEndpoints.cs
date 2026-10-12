using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Domain.Entities;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;

namespace TicketAnalytics.API.Endpoints;

public record CategoriaRequest(string Persid, string NombreCategoria, string TipoCaso, bool Activo = true);

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").WithTags("Administración");

        // Lista las categorias en alcance (base local)
        g.MapGet("/categorias", async (AnalyticsContext db, CancellationToken ct) =>
            Results.Ok(await db.CategoriasAlcance.AsNoTracking().OrderBy(c => c.NombreCategoria).ToListAsync(ct)))
         .WithSummary("Categorías configuradas en alcance (base local).");

        // Descubre categorias reales de MDB para copiar sus persid
        g.MapGet("/categorias-mdb", async (SdmReadContext sdm, string? filtro, CancellationToken ct) =>
        {
            var patron = string.IsNullOrWhiteSpace(filtro) ? "Tecnología de la Información.%Sistemas.%" : $"%{filtro}%";
            var cats = await sdm.ProbCtgs.AsNoTracking()
                .Where(p => EF.Functions.Like(p.sym!, patron))
                .Select(p => new { p.persid, p.sym })
                .OrderBy(p => p.sym).Take(200).ToListAsync(ct);
            return Results.Ok(new { total = cats.Count, categorias = cats });
        }).WithSummary("Busca categorías REALES en MDB (para obtener sus persid). Filtro opcional.");

        // Agrega una categoria al alcance
        g.MapPost("/categorias", async (CategoriaRequest req, AnalyticsContext db, CancellationToken ct) =>
        {
            var existe = await db.CategoriasAlcance.FindAsync(new object[] { req.Persid }, ct);
            if (existe != null)
            {
                existe.NombreCategoria = req.NombreCategoria;
                existe.TipoCaso = req.TipoCaso;
                existe.Activo = req.Activo;
            }
            else
            {
                db.CategoriasAlcance.Add(new CategoriaAlcance
                {
                    Persid = req.Persid, NombreCategoria = req.NombreCategoria,
                    TipoCaso = req.TipoCaso, Activo = req.Activo
                });
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { mensaje = existe != null ? "Categoría actualizada" : "Categoría agregada", req.Persid });
        }).WithSummary("Agrega o actualiza una categoría en el alcance.");

        g.MapDelete("/categorias/{persid}", async (string persid, AnalyticsContext db, CancellationToken ct) =>
        {
            var c = await db.CategoriasAlcance.FindAsync(new object[] { persid }, ct);
            if (c is null) return Results.NotFound();
            db.CategoriasAlcance.Remove(c);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithSummary("Quita una categoría del alcance.");

        // Prueba directa: cuenta tickets reales de MDB para las categorias en alcance
        g.MapGet("/prueba-mdb", async (AnalyticsContext db, SdmReadContext sdm, CancellationToken ct) =>
        {
            var alcance = await db.CategoriasAlcance.AsNoTracking().Where(c => c.Activo).Select(c => c.Persid).ToListAsync(ct);
            if (!alcance.Any())
                return Results.Ok(new { advertencia = "No hay categorías en alcance. Agrega con POST /api/admin/categorias.", tickets = 0 });

            var total = await sdm.CallReqs.AsNoTracking().CountAsync(cr => alcance.Contains(cr.category!), ct);
            var muestra = await sdm.CallReqs.AsNoTracking()
                .Where(cr => alcance.Contains(cr.category!))
                .OrderByDescending(cr => cr.open_date)
                .Select(cr => new { cr.ref_num, cr.category, cr.open_date, cr.status })
                .Take(5).ToListAsync(ct);

            return Results.Ok(new { categoriasEnAlcance = alcance, ticketsEncontradosEnMdb = total, muestra });
        }).WithSummary("PRUEBA: cuenta tickets reales de MDB para las categorías en alcance.");
    }
}
