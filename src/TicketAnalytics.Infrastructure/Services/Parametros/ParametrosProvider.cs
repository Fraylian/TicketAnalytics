using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Common.Models;
using TicketAnalytics.Infrastructure.Persistence.Analytics;

namespace TicketAnalytics.Infrastructure.Services.Parametros;

/// <summary>
/// Lee dbo.ParametroSistema (base LOCAL) y lo expone tipado, con caché de 5 minutos.
/// Singleton: crea su propio scope para usar el AnalyticsContext (que es scoped).
/// Si la tabla no responde, conserva el último valor conocido (o los de respaldo) y lo registra.
/// </summary>
public sealed class ParametrosProvider : IParametrosProvider
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ParametrosProvider> _logger;
    private readonly SemaphoreSlim _candado = new(1, 1);
    private ParametrosNegocio _ultimos = new();
    private DateTime _venceEn = DateTime.MinValue;

    public ParametrosProvider(IServiceScopeFactory scopes, ILogger<ParametrosProvider> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public ParametrosNegocio Ultimos => _ultimos;

    public void Invalidar() => _venceEn = DateTime.MinValue;

    public async Task<ParametrosNegocio> ObtenerAsync(CancellationToken ct = default)
    {
        if (DateTime.UtcNow < _venceEn) return _ultimos;

        await _candado.WaitAsync(ct);
        try
        {
            if (DateTime.UtcNow < _venceEn) return _ultimos;

            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsContext>();
            var valores = await db.ParametrosSistema.AsNoTracking()
                .ToDictionaryAsync(p => p.Clave, p => p.Valor, StringComparer.OrdinalIgnoreCase, ct);

            _ultimos = Construir(valores);
            _venceEn = DateTime.UtcNow.Add(Vigencia);
            return _ultimos;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "No se pudieron leer los parámetros de negocio; se usan los últimos conocidos.");
            _venceEn = DateTime.UtcNow.AddSeconds(30);   // reintento pronto, sin martillar la base
            return _ultimos;
        }
        finally { _candado.Release(); }
    }

    private ParametrosNegocio Construir(IReadOnlyDictionary<string, string> v)
    {
        var d = new ParametrosNegocio();   // solo respaldo para claves ausentes o inválidas

        int Entero(string clave, int respaldo, int min, int max)
        {
            if (v.TryGetValue(clave, out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max)
                return n;
            if (v.ContainsKey(clave))
                _logger.LogWarning("Parámetro {Clave} con valor inválido '{Valor}'; se usa {Respaldo}.", clave, v[clave], respaldo);
            return respaldo;
        }

        var fechaBase = d.FechaBase;
        if (v.TryGetValue("General.FechaBase", out var f))
        {
            if (DateTime.TryParseExact(f, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fb)) fechaBase = fb;
            else _logger.LogWarning("General.FechaBase '{Valor}' no tiene formato yyyy-MM-dd; se usa {Respaldo:yyyy-MM-dd}.", f, fechaBase);
        }

        return new ParametrosNegocio
        {
            FechaBase = fechaBase,
            OcDiasAlta = Entero("OC.DiasComplejidad.Alta", d.OcDiasAlta, 1, 3650),
            OcDiasMedia = Entero("OC.DiasComplejidad.Media", d.OcDiasMedia, 1, 3650),
            OcDiasBaja = Entero("OC.DiasComplejidad.Baja", d.OcDiasBaja, 1, 3650),
            OcDiasSinAsignar = Entero("OC.DiasComplejidad.SinAsignar", d.OcDiasSinAsignar, 1, 3650),
            OcDiasVencePronto = Entero("OC.DiasVencePronto", d.OcDiasVencePronto, 0, 365),
            OcDiasMargenVentana = Entero("OC.DiasMargenVentana", d.OcDiasMargenVentana, 0, 365),
            TicketDiasVencePronto = Entero("Ticket.DiasVencePronto", d.TicketDiasVencePronto, 0, 365),
            TicketPctRiesgoSla = Entero("Ticket.PctRiesgoSla", d.TicketPctRiesgoSla, 1, 100),
            AlertasMinutosAntelacion = Entero("Alertas.MinutosAntelacion", d.AlertasMinutosAntelacion, 1, 1440),
            AlertasMinutosAvisoTemprano = Entero("Alertas.MinutosAvisoTemprano", d.AlertasMinutosAvisoTemprano, 1, 2880),
            SeguimientoDiasSinAvance = Entero("Seguimiento.DiasSinAvance", d.SeguimientoDiasSinAvance, 1, 365),
            CacheCatalogosMinutos = Entero("Cache.CatalogosMinutos", d.CacheCatalogosMinutos, 1, 1440),
            ConsultasUmbralLentaMs = Entero("Consultas.UmbralLentaMs", d.ConsultasUmbralLentaMs, 50, 600000)
        };
    }
}
