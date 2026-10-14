using TicketAnalytics.Web.Models;

namespace TicketAnalytics.Web.Services;

/// <summary>
/// Cache en memoria (scoped) para catálogos que cambian poco: analistas del grupo,
/// proveedores, categorías VP, estados. Evita repetir el mismo GET en cada pantalla.
/// NO cachea datos operativos (tickets, OC, seguimientos): esos deben venir frescos.
/// </summary>
public sealed class CatalogoCache
{
    private static readonly TimeSpan TtlPorDefecto = TimeSpan.FromMinutes(10);
    private readonly Dictionary<string, (object Valor, DateTime Expira)> _items = new();

    /// <summary>Devuelve el valor cacheado o ejecuta la carga y lo guarda.</summary>
    public async Task<T> ObtenerAsync<T>(string clave, Func<Task<T>> cargar, TimeSpan? ttl = null)
    {
        if (_items.TryGetValue(clave, out var item) && item.Expira > DateTime.UtcNow && item.Valor is T tipado)
            return tipado;

        var valor = await cargar();
        if (valor is not null)
            _items[clave] = (valor, DateTime.UtcNow.Add(ttl ?? TtlPorDefecto));
        return valor;
    }

    /// <summary>Invalida una clave concreta (tras editar ese catálogo).</summary>
    public void Invalidar(string clave) => _items.Remove(clave);

    /// <summary>Invalida todas las claves que empiecen con el prefijo dado.</summary>
    public void InvalidarPrefijo(string prefijo)
    {
        foreach (var k in _items.Keys.Where(k => k.StartsWith(prefijo, StringComparison.Ordinal)).ToList())
            _items.Remove(k);
    }

    public void LimpiarTodo() => _items.Clear();

    // Claves conocidas (evita strings mágicos repartidos por las páginas)
    public const string Analistas = "catalogo:analistas";
    public const string Proveedores = "catalogo:proveedores";
    public const string CategoriasVP = "catalogo:categorias-vp";
    public const string Parametros = "catalogo:parametros";

    /// <summary>Parámetros de negocio (cacheados 10 min). Si la API falla se usan los de respaldo.</summary>
    public async Task<ParametrosNegocioDto> ParametrosAsync(TicketAnalyticsApi api)
    {
        try { return await ObtenerAsync(Parametros, () => api.ParametrosAsync()); }
        catch { return new ParametrosNegocioDto(); }
    }
}
