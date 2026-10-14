using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Sistemas.DTOs;
using TicketAnalytics.Domain.Enums;

namespace TicketAnalytics.API.Endpoints;

/// <summary>Catálogos de la clasificación de la gestión. Sistema y tipo de corrección viven
/// en la base local y se administran aquí; tipo de solución y subtipo de actualización siguen
/// siendo valores fijos del dominio.</summary>
public static class CatalogoGestionEndpoints
{
    public static void MapCatalogoGestionEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/catalogos-gestion").WithTags("Catálogos de gestión");

        // Todo en una sola llamada, para cachear del lado del cliente.
        g.MapGet("/todos", async (ISistemaCatalogoService s, CancellationToken ct) => Results.Ok(await s.CatalogosGestionAsync(ct)))
         .WithSummary("Sistemas (con su proveedor), proveedores, tipos de solución, subtipos de actualización y tipos de corrección activos.");

        g.MapGet("/tipos-solucion", () =>
            Results.Ok(TipoSolucionInfo.Opciones().Select(o => new { valor = o.Valor, nombre = o.Nombre })))
         .WithSummary("Tipos de solución (estructurado).");

        g.MapGet("/tipos-actualizacion-datos", () =>
            Results.Ok(TipoActualizacionDatosInfo.Opciones().Select(o => new { valor = o.Valor, nombre = o.Nombre })))
         .WithSummary("Subtipos de actualización de datos (solo sistemas que lo exigen + Actualización de datos).");

        // ===== Sistemas (eje de clasificación, N:1 con proveedor) =====
        var si = app.MapGroup("/api/sistemas").WithTags("Sistemas");

        si.MapGet("/", async (ISistemaCatalogoService s, CancellationToken ct) => Results.Ok(await s.ListarSistemasAsync(ct)))
          .WithSummary("Catálogo de sistemas con su proveedor responsable (incluye inactivos).");

        si.MapPost("/guardar", async (GuardarSistemaRequest req, ISistemaCatalogoService s, CancellationToken ct) =>
            Results.Ok(await s.GuardarSistemaAsync(req, ct)))
          .WithSummary("Crea o edita un sistema (nombre único; el proveedor debe existir).");

        si.MapDelete("/{sistemaId:int}", async (int sistemaId, ISistemaCatalogoService s, CancellationToken ct) =>
            Results.Ok(await s.EliminarSistemaAsync(sistemaId, ct)))
          .WithSummary("Elimina el sistema; si ya tiene casos o actividades, solo lo desactiva.");

        // ===== Tipos de corrección =====
        var tc = app.MapGroup("/api/tipos-correccion").WithTags("Tipos de corrección");

        tc.MapGet("/", async (ISistemaCatalogoService s, CancellationToken ct) => Results.Ok(await s.ListarTiposCorreccionAsync(ct)))
          .WithSummary("Catálogo de tipos de corrección (incluye inactivos).");

        tc.MapPost("/guardar", async (GuardarTipoCorreccionRequest req, ISistemaCatalogoService s, CancellationToken ct) =>
            Results.Ok(await s.GuardarTipoCorreccionAsync(req, ct)))
          .WithSummary("Crea o edita un tipo de corrección.");

        tc.MapDelete("/{id:int}", async (int id, ISistemaCatalogoService s, CancellationToken ct) =>
            Results.Ok(await s.EliminarTipoCorreccionAsync(id, ct)))
          .WithSummary("Elimina el tipo de corrección; si ya está en uso, solo lo desactiva.");
    }
}
