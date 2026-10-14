using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Domain.Common;
using TicketAnalytics.Domain.Entities;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;
using TicketAnalytics.Infrastructure.Services.Parametros;

namespace TicketAnalytics.API.Endpoints;

public record CategoriaRequest(string Persid, string NombreCategoria, string TipoCaso, bool Activo = true);
public record ParametroRequest(string Valor, string? Usuario);
public record AnalistaGrupoRequest(bool Activo = true, int Orden = 0);

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").WithTags("Administración");

        // Valores de negocio que necesita la interfaz (fecha base, umbrales de alerta). Solo lectura.
        app.MapGet("/api/parametros", async (IParametrosProvider parametros, CancellationToken ct) =>
            Results.Ok(await parametros.ObtenerAsync(ct)))
         .WithTags("Administración").WithSummary("Parámetros de negocio vigentes (tipados).");

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
        g.MapPost("/categorias", async (CategoriaRequest req, AnalyticsContext db, CatalogoSdm catalogo, CancellationToken ct) =>
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
            catalogo.Invalidar();   // el alcance cacheado debe reflejar el cambio de inmediato
            return Results.Ok(new { mensaje = existe != null ? "Categoría actualizada" : "Categoría agregada", req.Persid });
        }).WithSummary("Agrega o actualiza una categoría en el alcance.");

        g.MapDelete("/categorias/{persid}", async (string persid, AnalyticsContext db, CatalogoSdm catalogo, CancellationToken ct) =>
        {
            var c = await db.CategoriasAlcance.FindAsync(new object[] { persid }, ct);
            if (c is null) return Results.NotFound();
            db.CategoriasAlcance.Remove(c);
            await db.SaveChangesAsync(ct);
            catalogo.Invalidar();
            return Results.NoContent();
        }).WithSummary("Quita una categoría del alcance.");

        // Prueba directa: cuenta tickets reales de MDB para las categorias en alcance.
        // Acotada desde la fecha base (call_req_x0 por open_date) para no recorrer todo el histórico.
        g.MapGet("/prueba-mdb", async (CatalogoSdm catalogo, IParametrosProvider parametros, SdmReadContext sdm, CancellationToken ct) =>
        {
            var alcance = (await catalogo.AlcanceAsync(ct)).Select(a => a.Persid).ToList();
            if (!alcance.Any())
                return Results.Ok(new { advertencia = "No hay categorías en alcance. Agrega con POST /api/admin/categorias.", tickets = 0 });

            var p = await parametros.ObtenerAsync(ct);
            var desde = EpochRD.InicioDelDia(p.FechaBase);
            var q = sdm.CallReqs.AsNoTracking().Where(cr => alcance.Contains(cr.category!) && cr.open_date >= desde);
            var total = await q.CountAsync(ct);
            var muestra = await q.OrderByDescending(cr => cr.open_date)
                .Select(cr => new { cr.ref_num, cr.category, cr.open_date, cr.status })
                .Take(5).ToListAsync(ct);

            return Results.Ok(new { categoriasEnAlcance = alcance, desde = p.FechaBase.ToString("yyyy-MM-dd"), ticketsEncontradosEnMdb = total, muestra });
        }).WithSummary("PRUEBA: cuenta tickets de MDB del alcance abiertos desde la fecha base.");

        // ---- Parámetros de negocio (dbo.ParametroSistema, base local) ----
        g.MapGet("/parametros", async (AnalyticsContext db, CancellationToken ct) =>
            Results.Ok(await db.ParametrosSistema.AsNoTracking().OrderBy(x => x.Clave).ToListAsync(ct)))
         .WithSummary("Parámetros de negocio configurables.");

        g.MapPut("/parametros/{clave}", async (string clave, ParametroRequest req, AnalyticsContext db,
                                                IParametrosProvider parametros, CatalogoSdm catalogo, CancellationToken ct) =>
        {
            var par = await db.ParametrosSistema.FindAsync(new object[] { clave }, ct);
            if (par is null) return Results.NotFound(new { error = $"No existe el parámetro '{clave}'." });

            var esFecha = clave.Equals("General.FechaBase", StringComparison.OrdinalIgnoreCase);
            var valor = (req.Valor ?? "").Trim();
            var valido = esFecha
                ? DateTime.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                : int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0;
            if (!valido)
                return Results.BadRequest(new { error = esFecha ? "La fecha base debe tener formato yyyy-MM-dd." : "El valor debe ser un entero mayor o igual a 0." });

            par.Valor = valor;
            par.ActualizadoEn = DateTime.UtcNow;
            par.ActualizadoPor = string.IsNullOrWhiteSpace(req.Usuario) ? null : req.Usuario.Trim();
            await db.SaveChangesAsync(ct);
            parametros.Invalidar();
            catalogo.Invalidar();   // la vigencia de la caché de catálogos también es un parámetro
            return Results.Ok(par);
        }).WithSummary("Cambia el valor de un parámetro (sin recompilar). Invalida las cachés.");

        // ---- Grupo de analistas (dbo.AnalistaGrupo, base local) ----
        g.MapGet("/grupo", async (AnalyticsContext db, IGrupoAnalistasProvider grupo, CancellationToken ct) =>
        {
            var config = await db.AnalistasGrupo.AsNoTracking().OrderBy(a => a.Orden).ToListAsync(ct);
            var resueltos = (await grupo.ObtenerAsync(ct)).ToDictionary(m => m.UserId, StringComparer.OrdinalIgnoreCase);
            return Results.Ok(config.Select(a =>
            {
                resueltos.TryGetValue(a.UserId, out var m);
                return new { a.UserId, a.Activo, a.Orden, Nombre = m?.Nombre, Uuid = m?.UuidHex, ExisteEnMdb = m != null };
            }));
        }).WithSummary("Miembros configurados del grupo, con su nombre resuelto en MDB (solo los activos se resuelven).");

        g.MapPut("/grupo/{userId}", async (string userId, AnalistaGrupoRequest req, AnalyticsContext db,
                                           IGrupoAnalistasProvider grupo, CancellationToken ct) =>
        {
            userId = userId.Trim();
            if (userId.Length == 0) return Results.BadRequest(new { error = "userid vacío." });
            var a = await db.AnalistasGrupo.FindAsync(new object[] { userId }, ct);
            if (a is null) db.AnalistasGrupo.Add(a = new AnalistaGrupo { UserId = userId });
            a.Activo = req.Activo;
            a.Orden = req.Orden;
            await db.SaveChangesAsync(ct);
            grupo.Invalidar();
            return Results.Ok(new { a.UserId, a.Activo, a.Orden });
        }).WithSummary("Agrega, activa o desactiva un analista del grupo (por userid de ca_contact).");
    }
}
