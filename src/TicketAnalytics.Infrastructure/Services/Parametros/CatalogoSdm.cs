using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Common.Models;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;

namespace TicketAnalytics.Infrastructure.Services.Parametros;

/// <summary>Categoría del alcance (base local) con su nombre legible de MDB.</summary>
public sealed record CategoriaTicket(string Persid, string? Sym, string TipoCaso);

/// <summary>Nombre de un contacto de MDB (persona o grupo: los grupos guardan su nombre en last_name).</summary>
public sealed record NombreContacto(string? Nombre, string? Apellido)
{
    public string? Completo => string.IsNullOrWhiteSpace($"{Nombre} {Apellido}") ? null : $"{Nombre} {Apellido}".Trim();
}

/// <summary>
/// Caché en memoria (singleton) de catálogos de MDB que casi no cambian: categorías del alcance,
/// estados de ticket y de OC, categorías de OC del área y nombres de contactos.
///
/// Antes cada página de resultados volvía a pedir estos catálogos (6-8 viajes a MDB por request).
/// Ahora se leen una vez cada "Cache.CatalogosMinutos" (parámetro) y los contactos se piden solo
/// cuando aparece un UUID que todavía no está en memoria.
/// </summary>
public sealed class CatalogoSdm
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IParametrosProvider _parametros;
    private readonly ILogger<CatalogoSdm> _logger;

    private readonly SemaphoreSlim _candado = new(1, 1);
    private Catalogos? _catalogos;
    private DateTime _catalogosVencen = DateTime.MinValue;

    private readonly ConcurrentDictionary<string, (NombreContacto Valor, DateTime Vence)> _contactos = new();
    private readonly SemaphoreSlim _candadoGrupo = new(1, 1);
    private IReadOnlyList<MiembroGrupo>? _grupo;
    private DateTime _grupoVence = DateTime.MinValue;

    public CatalogoSdm(IServiceScopeFactory scopes, IParametrosProvider parametros, ILogger<CatalogoSdm> logger)
    {
        _scopes = scopes;
        _parametros = parametros;
        _logger = logger;
    }

    private sealed record Catalogos(
        IReadOnlyList<CategoriaTicket> Alcance,
        IReadOnlyDictionary<string, string> EstadosTicket,
        IReadOnlyDictionary<string, string> EstadosOc,
        IReadOnlyDictionary<string, string?> CategoriasOcArea,
        IReadOnlyDictionary<string, string?> SymCategoriasOc);

    public void Invalidar()
    {
        _catalogosVencen = DateTime.MinValue;
        _grupoVence = DateTime.MinValue;
        _contactos.Clear();
    }

    private async Task<TimeSpan> VigenciaAsync(CancellationToken ct)
        => TimeSpan.FromMinutes((await _parametros.ObtenerAsync(ct)).CacheCatalogosMinutos);

    private async Task<Catalogos> CatalogosAsync(CancellationToken ct)
    {
        if (_catalogos is not null && DateTime.UtcNow < _catalogosVencen) return _catalogos;
        await _candado.WaitAsync(ct);
        try
        {
            if (_catalogos is not null && DateTime.UtcNow < _catalogosVencen) return _catalogos;

            using var scope = _scopes.CreateScope();
            var sdm = scope.ServiceProvider.GetRequiredService<SdmReadContext>();
            var local = scope.ServiceProvider.GetRequiredService<AnalyticsContext>();

            // Alcance (base local) + nombre legible desde prob_ctg (índice único prob_ctg_x1 por persid).
            var alcanceLocal = await local.CategoriasAlcance.AsNoTracking()
                .Where(c => c.Activo).Select(c => new { c.Persid, c.TipoCaso }).ToListAsync(ct);
            var persids = alcanceLocal.Select(a => a.Persid).ToList();
            var syms = persids.Count == 0
                ? new Dictionary<string, string?>()
                : await sdm.ProbCtgs.AsNoTracking().Where(p => persids.Contains(p.persid))
                    .Select(p => new { p.persid, p.sym }).ToDictionaryAsync(p => p.persid, p => p.sym, ct);
            var alcance = alcanceLocal
                .Select(a => new CategoriaTicket(a.Persid, syms.TryGetValue(a.Persid, out var s) ? s : null, a.TipoCaso))
                .ToList();

            // cr_stat y chgstat: tablas chicas de catálogo (decenas de filas).
            var estTicket = await sdm.CrStats.AsNoTracking()
                .Select(s => new { s.code, Texto = s.description ?? s.sym ?? s.code })
                .ToDictionaryAsync(s => s.code, s => s.Texto, ct);
            var estOc = await sdm.ChgStats.AsNoTracking()
                .Select(s => new { s.code, Texto = s.description ?? s.sym ?? s.code })
                .ToDictionaryAsync(s => s.code, s => s.Texto, ct);

            // Categorías de OC del área (chg.category = chgcat.code). LIKE sobre chgcat: tabla de catálogo.
            var ocArea = await sdm.ChgCats.AsNoTracking()
                .Where(c => c.sym != null && c.sym.Contains("Orden de Cambio"))
                .Select(c => new { c.code, c.sym }).ToDictionaryAsync(c => c.code, c => c.sym, ct);

            _catalogos = new Catalogos(alcance, estTicket, estOc, ocArea, ocArea);
            _catalogosVencen = DateTime.UtcNow.Add(await VigenciaAsync(ct));
            _logger.LogInformation("Catálogos de MDB cargados en caché: {Alcance} categorías, {Est} estados de ticket, {EstOc} estados de OC, {Oc} categorías de OC.",
                alcance.Count, estTicket.Count, estOc.Count, ocArea.Count);
            return _catalogos;
        }
        finally { _candado.Release(); }
    }

    /// <summary>Categorías activas del alcance (persid + nombre + tipo de caso).</summary>
    public async Task<IReadOnlyList<CategoriaTicket>> AlcanceAsync(CancellationToken ct = default)
        => (await CatalogosAsync(ct)).Alcance;

    /// <summary>Texto legible de un estado de ticket (cr_stat.code).</summary>
    public async Task<IReadOnlyDictionary<string, string>> EstadosTicketAsync(CancellationToken ct = default)
        => (await CatalogosAsync(ct)).EstadosTicket;

    /// <summary>Texto legible de un estado de OC (chgstat.code).</summary>
    public async Task<IReadOnlyDictionary<string, string>> EstadosOcAsync(CancellationToken ct = default)
        => (await CatalogosAsync(ct)).EstadosOc;

    /// <summary>Categorías de OC del área: chgcat.code → sym.</summary>
    public async Task<IReadOnlyDictionary<string, string?>> CategoriasOcAreaAsync(CancellationToken ct = default)
        => (await CatalogosAsync(ct)).CategoriasOcArea;

    /// <summary>Nombre legible de categorías de ticket del alcance (persid → sym).</summary>
    public async Task<IReadOnlyDictionary<string, string?>> SymCategoriasTicketAsync(CancellationToken ct = default)
        => (await CatalogosAsync(ct)).Alcance.ToDictionary(a => a.Persid, a => a.Sym);

    /// <summary>
    /// Nombres de contactos. Solo consulta MDB por los UUID que no están en memoria
    /// (búsqueda por la clave primaria agrupada de ca_contact).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, NombreContacto>> ContactosAsync(IEnumerable<byte[]?> uuids, CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        var pedidos = uuids.Where(u => u is { Length: > 0 }).Select(u => u!)
            .GroupBy(Convert.ToHexString).Select(g => (Hex: g.Key, Bytes: g.First())).ToList();

        var resultado = new Dictionary<string, NombreContacto>(StringComparer.OrdinalIgnoreCase);
        var faltan = new List<(string Hex, byte[] Bytes)>();
        foreach (var p in pedidos)
        {
            if (_contactos.TryGetValue(p.Hex, out var c) && c.Vence > ahora) resultado[p.Hex] = c.Valor;
            else faltan.Add(p);
        }
        if (faltan.Count == 0) return resultado;

        var vigencia = await VigenciaAsync(ct);
        using var scope = _scopes.CreateScope();
        var sdm = scope.ServiceProvider.GetRequiredService<SdmReadContext>();
        foreach (var lote in faltan.Chunk(500))
        {
            var bytes = lote.Select(l => l.Bytes).ToList();
            var filas = await sdm.CaContacts.AsNoTracking()
                .Where(c => bytes.Contains(c.contact_uuid))
                .Select(c => new { c.contact_uuid, c.first_name, c.last_name })
                .ToListAsync(ct);
            foreach (var f in filas)
            {
                var hex = Convert.ToHexString(f.contact_uuid);
                var valor = new NombreContacto(f.first_name, f.last_name);
                _contactos[hex] = (valor, ahora.Add(vigencia));
                resultado[hex] = valor;
            }
        }
        return resultado;
    }

    /// <summary>
    /// UUIDs de los contactos que corresponden a un nombre escrito en la UI.
    /// 1) Primero en el grupo de analistas (en memoria, sin ir a MDB): es el caso normal, porque
    ///    los selectores de la aplicación ofrecen esos nombres.
    /// 2) Si nadie del grupo coincide, busca en ca_contact por el token más largo (LIKE acotado a
    ///    200 filas) y afina en memoria. Antes esta búsqueda se hacía SIEMPRE y además se traían
    ///    todos los tickets del rango para comparar los UUID en C#.
    /// contiene = true: el nombre completo debe contener el texto (regla del módulo de OC).
    /// contiene = false: coincidencia por tokens (regla del módulo de tickets).
    /// </summary>
    public async Task<List<byte[]>> UuidsPorNombreAsync(string nombre, bool contiene, CancellationToken ct = default)
    {
        nombre = nombre.Trim();
        var tokens = CatalogoSdmNombres.Tokens(nombre);
        bool Coincide(string completo, string? userid)
            => contiene
                ? completo.Contains(nombre, StringComparison.OrdinalIgnoreCase)
                : string.Equals(userid, nombre, StringComparison.OrdinalIgnoreCase)
                  || (tokens.Count > 0 && CatalogoSdmNombres.CoincidePorTokens($"{completo} {userid}", tokens));

        var grupo = await GrupoAsync(ct);
        var delGrupo = grupo.Where(m => Coincide(m.Nombre, m.UserId)).Select(m => m.Uuid).ToList();
        if (delGrupo.Count > 0) return delGrupo;

        var ancla = (tokens.Count > 0 ? tokens.OrderByDescending(t => t.Length).First() : nombre);
        if (ancla.Length < 3) return new();

        using var scope = _scopes.CreateScope();
        var sdm = scope.ServiceProvider.GetRequiredService<SdmReadContext>();
        var candidatos = await sdm.CaContacts.AsNoTracking()
            .Where(x => (x.first_name ?? "").Contains(ancla) || (x.last_name ?? "").Contains(ancla) || (x.userid ?? "").Contains(ancla))
            .Select(x => new { x.contact_uuid, x.first_name, x.last_name, x.userid })
            .Take(200)
            .ToListAsync(ct);
        return candidatos.Where(x => Coincide($"{x.first_name} {x.last_name}".Trim(), x.userid))
                         .Select(x => x.contact_uuid).Take(50).ToList();
    }

    /// <summary>
    /// Grupo de analistas: userid activos de dbo.AnalistaGrupo (base local) resueltos contra
    /// ca_contact (índice ca_contact_idx_03 por userid). Se cachea con la vigencia de catálogos.
    /// </summary>
    public async Task<IReadOnlyList<MiembroGrupo>> GrupoAsync(CancellationToken ct = default)
    {
        if (_grupo is not null && DateTime.UtcNow < _grupoVence) return _grupo;
        await _candadoGrupo.WaitAsync(ct);
        try
        {
            if (_grupo is not null && DateTime.UtcNow < _grupoVence) return _grupo;

            using var scope = _scopes.CreateScope();
            var local = scope.ServiceProvider.GetRequiredService<AnalyticsContext>();
            var sdm = scope.ServiceProvider.GetRequiredService<SdmReadContext>();

            var userIds = await local.AnalistasGrupo.AsNoTracking().Where(a => a.Activo)
                .OrderBy(a => a.Orden).Select(a => a.UserId).ToListAsync(ct);

            var contactos = userIds.Count == 0
                ? new()
                : await sdm.CaContacts.AsNoTracking()
                    .Where(c => c.userid != null && userIds.Contains(c.userid))
                    .Select(c => new { c.userid, c.first_name, c.last_name, c.contact_uuid })
                    .ToListAsync(ct);

            var grupo = new List<MiembroGrupo>();
            foreach (var uid in userIds)
            {
                var c = contactos.FirstOrDefault(x => string.Equals(x.userid, uid, StringComparison.OrdinalIgnoreCase));
                if (c is null)
                {
                    _logger.LogWarning("El analista {UserId} del grupo no existe en ca_contact de MDB.", uid);
                    continue;
                }
                grupo.Add(new MiembroGrupo(uid, $"{c.first_name} {c.last_name}".Trim(), c.contact_uuid));
            }

            _grupo = grupo;
            _grupoVence = DateTime.UtcNow.Add(await VigenciaAsync(ct));
            return _grupo;
        }
        finally { _candadoGrupo.Release(); }
    }
}

public static class CatalogoSdmNombres
{
    /// <summary>"Nombre Apellido" con varios tokens: coinciden si comparten al menos 2 (o 1 si solo hay uno).</summary>
    public static bool CoincidePorTokens(string completo, IReadOnlyList<string> tokens)
    {
        var aciertos = tokens.Count(t => completo.Contains(t, StringComparison.OrdinalIgnoreCase));
        return tokens.Count == 1 ? aciertos >= 1 : aciertos >= 2;
    }

    public static List<string> Tokens(string nombre)
        => nombre.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 2).ToList();
}

/// <summary>Adaptador del grupo para la capa Application.</summary>
public sealed class GrupoAnalistasProvider : IGrupoAnalistasProvider
{
    private readonly CatalogoSdm _catalogo;
    public GrupoAnalistasProvider(CatalogoSdm catalogo) => _catalogo = catalogo;
    public Task<IReadOnlyList<MiembroGrupo>> ObtenerAsync(CancellationToken ct = default) => _catalogo.GrupoAsync(ct);
    public void Invalidar() => _catalogo.Invalidar();
}
