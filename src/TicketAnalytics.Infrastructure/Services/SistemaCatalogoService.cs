using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Sistemas.DTOs;
using TicketAnalytics.Domain.Entities;
using TicketAnalytics.Domain.Enums;
using TicketAnalytics.Infrastructure.Persistence.Analytics;

namespace TicketAnalytics.Infrastructure.Services;

/// <summary>
/// Administración de los catálogos locales de la clasificación: sistemas (con su proveedor
/// responsable, N:1) y tipos de corrección. Todo es base local; nada toca MDB.
/// Un elemento con casos asociados no se borra: se desactiva, para no romper el histórico.
/// </summary>
public class SistemaCatalogoService : ISistemaCatalogoService
{
    private readonly AnalyticsContext _db;

    public SistemaCatalogoService(AnalyticsContext db) => _db = db;

    // ================= Sistemas =================

    public async Task<IReadOnlyList<SistemaDto>> ListarSistemasAsync(CancellationToken ct = default)
        => await _db.Sistemas.AsNoTracking()
            .OrderBy(s => s.Orden).ThenBy(s => s.Nombre)
            .Select(s => new SistemaDto
            {
                SistemaId = s.SistemaId,
                Nombre = s.Nombre,
                Descripcion = s.Descripcion,
                ProveedorId = s.ProveedorId,
                Proveedor = s.Proveedor != null ? s.Proveedor.Nombre : null,
                Criticidad = s.Criticidad,
                Activo = s.Activo,
                Orden = s.Orden,
                Color = s.Color,
                AplicaSubtipoActualizacion = s.AplicaSubtipoActualizacion,
                CasosClasificados = s.Seguimientos.Count()
            })
            .ToListAsync(ct);

    public async Task<SistemaDto> GuardarSistemaAsync(GuardarSistemaRequest req, CancellationToken ct = default)
    {
        var nombre = (req.Nombre ?? "").Trim();
        if (nombre.Length == 0) throw new InvalidOperationException("El nombre del sistema es obligatorio.");
        if (nombre.Length > 120) throw new InvalidOperationException("El nombre no puede pasar de 120 caracteres.");
        if (req.Criticidad is < 0 or > 3) throw new InvalidOperationException("La criticidad va de 0 (sin definir) a 3 (alta).");
        if (await _db.Sistemas.AnyAsync(s => s.Nombre == nombre && s.SistemaId != (req.SistemaId ?? 0), ct))
            throw new InvalidOperationException($"Ya existe un sistema llamado \"{nombre}\".");
        if (req.ProveedorId is > 0 && !await _db.Proveedores.AnyAsync(p => p.ProveedorId == req.ProveedorId, ct))
            throw new InvalidOperationException("El proveedor elegido no existe.");

        Sistema s;
        if (req.SistemaId is > 0)
        {
            s = await _db.Sistemas.FirstOrDefaultAsync(x => x.SistemaId == req.SistemaId, ct)
                ?? throw new KeyNotFoundException("El sistema a editar no existe.");
        }
        else
        {
            s = new Sistema();
            _db.Sistemas.Add(s);
        }

        s.Nombre = nombre;
        s.Descripcion = Recortar(req.Descripcion, 400);
        s.ProveedorId = req.ProveedorId is > 0 ? req.ProveedorId : null;
        s.Criticidad = req.Criticidad;
        s.Activo = req.Activo;
        s.Orden = req.Orden;
        s.Color = Recortar(req.Color, 20);
        s.AplicaSubtipoActualizacion = req.AplicaSubtipoActualizacion;

        await _db.SaveChangesAsync(ct);
        return (await ListarSistemasAsync(ct)).First(x => x.SistemaId == s.SistemaId);
    }

    public async Task<ResultadoEliminacionDto> EliminarSistemaAsync(int sistemaId, CancellationToken ct = default)
    {
        var s = await _db.Sistemas.FirstOrDefaultAsync(x => x.SistemaId == sistemaId, ct)
                ?? throw new KeyNotFoundException("El sistema no existe.");

        var usos = await _db.TicketSeguimientos.CountAsync(x => x.SistemaId == sistemaId, ct)
                 + await _db.ActividadesDiarias.CountAsync(x => x.SistemaId == sistemaId, ct);
        if (usos > 0)
        {
            s.Activo = false;
            await _db.SaveChangesAsync(ct);
            return new ResultadoEliminacionDto
            {
                Desactivado = true,
                Mensaje = $"\"{s.Nombre}\" tiene {usos} registro(s) clasificados: se desactivó para conservar el histórico."
            };
        }

        _db.Sistemas.Remove(s);
        await _db.SaveChangesAsync(ct);
        return new ResultadoEliminacionDto { Eliminado = true, Mensaje = $"\"{s.Nombre}\" se eliminó." };
    }

    // ================= Tipos de corrección =================

    public async Task<IReadOnlyList<TipoCorreccionDto>> ListarTiposCorreccionAsync(CancellationToken ct = default)
        => await _db.TiposCorreccion.AsNoTracking()
            .OrderBy(t => t.Orden).ThenBy(t => t.Nombre)
            .Select(t => new TipoCorreccionDto
            {
                TipoCorreccionId = t.TipoCorreccionId,
                Nombre = t.Nombre,
                Descripcion = t.Descripcion,
                Activo = t.Activo,
                Orden = t.Orden,
                Usos = _db.TicketSeguimientos.Count(x => x.TipoCorreccionId == t.TipoCorreccionId)
            })
            .ToListAsync(ct);

    public async Task<TipoCorreccionDto> GuardarTipoCorreccionAsync(GuardarTipoCorreccionRequest req, CancellationToken ct = default)
    {
        var nombre = (req.Nombre ?? "").Trim();
        if (nombre.Length == 0) throw new InvalidOperationException("El nombre es obligatorio.");
        if (nombre.Length > 120) throw new InvalidOperationException("El nombre no puede pasar de 120 caracteres.");
        if (await _db.TiposCorreccion.AnyAsync(t => t.Nombre == nombre && t.TipoCorreccionId != (req.TipoCorreccionId ?? 0), ct))
            throw new InvalidOperationException($"Ya existe un tipo de corrección llamado \"{nombre}\".");

        TipoCorreccion t;
        if (req.TipoCorreccionId is > 0)
        {
            t = await _db.TiposCorreccion.FirstOrDefaultAsync(x => x.TipoCorreccionId == req.TipoCorreccionId, ct)
                ?? throw new KeyNotFoundException("El tipo de corrección a editar no existe.");
        }
        else
        {
            t = new TipoCorreccion();
            _db.TiposCorreccion.Add(t);
        }

        t.Nombre = nombre;
        t.Descripcion = Recortar(req.Descripcion, 400);
        t.Activo = req.Activo;
        t.Orden = req.Orden;
        await _db.SaveChangesAsync(ct);
        return (await ListarTiposCorreccionAsync(ct)).First(x => x.TipoCorreccionId == t.TipoCorreccionId);
    }

    public async Task<ResultadoEliminacionDto> EliminarTipoCorreccionAsync(int id, CancellationToken ct = default)
    {
        var t = await _db.TiposCorreccion.FirstOrDefaultAsync(x => x.TipoCorreccionId == id, ct)
                ?? throw new KeyNotFoundException("El tipo de corrección no existe.");
        var usos = await _db.TicketSeguimientos.CountAsync(x => x.TipoCorreccionId == id, ct);
        if (usos > 0)
        {
            t.Activo = false;
            await _db.SaveChangesAsync(ct);
            return new ResultadoEliminacionDto
            {
                Desactivado = true,
                Mensaje = $"\"{t.Nombre}\" está en {usos} caso(s): se desactivó para conservar el histórico."
            };
        }
        _db.TiposCorreccion.Remove(t);
        await _db.SaveChangesAsync(ct);
        return new ResultadoEliminacionDto { Eliminado = true, Mensaje = $"\"{t.Nombre}\" se eliminó." };
    }

    // ================= Catálogos para los formularios =================

    public async Task<CatalogosGestionDto> CatalogosGestionAsync(CancellationToken ct = default)
    {
        var sistemas = await _db.Sistemas.AsNoTracking()
            .Where(s => s.Activo)
            .OrderBy(s => s.Orden).ThenBy(s => s.Nombre)
            .Select(s => new SistemaOpcionDto
            {
                Valor = s.SistemaId,
                Nombre = s.Nombre,
                ProveedorId = s.ProveedorId,
                Proveedor = s.Proveedor != null ? s.Proveedor.Nombre : null,
                Color = s.Color,
                AplicaSubtipoActualizacion = s.AplicaSubtipoActualizacion
            })
            .ToListAsync(ct);

        var proveedores = await _db.Proveedores.AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new OpcionDto { Valor = p.ProveedorId, Nombre = p.Nombre })
            .ToListAsync(ct);

        var correcciones = await _db.TiposCorreccion.AsNoTracking()
            .Where(t => t.Activo)
            .OrderBy(t => t.Orden).ThenBy(t => t.Nombre)
            .Select(t => new OpcionDto { Valor = t.TipoCorreccionId, Nombre = t.Nombre })
            .ToListAsync(ct);

        return new CatalogosGestionDto
        {
            Sistemas = sistemas,
            Proveedores = proveedores,
            TiposCorreccion = correcciones,
            TiposSolucion = TipoSolucionInfo.Opciones().Select(o => new OpcionDto { Valor = o.Valor, Nombre = o.Nombre }).ToList(),
            TiposActualizacion = TipoActualizacionDatosInfo.Opciones().Select(o => new OpcionDto { Valor = o.Valor, Nombre = o.Nombre }).ToList()
        };
    }

    private static string? Recortar(string? s, int max)
    {
        var t = s?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length > max ? t[..max] : t;
    }
}
