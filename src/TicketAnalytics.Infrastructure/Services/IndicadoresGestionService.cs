using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Dashboard.DTOs;
using TicketAnalytics.Domain.Enums;
using TicketAnalytics.Infrastructure.Persistence.Analytics;

namespace TicketAnalytics.Infrastructure.Services;

/// <summary>
/// Indicadores construidos sobre la clasificación estructurada de la gestión.
/// Todo sale de la base local (TicketSeguimiento): sistema, tipo de solución,
/// tipo de actualización de datos, proveedor, causa raíz y VP.
/// Proyecta solo las columnas necesarias; no materializa entidades completas.
/// </summary>
public class IndicadoresGestionService : IIndicadoresGestionService
{
    private readonly AnalyticsContext _db;

    public IndicadoresGestionService(AnalyticsContext db) => _db = db;

    /// <summary>Fila mínima que necesitamos para todos los cortes.</summary>
    private sealed record Fila(
        SistemaAfectado Sistema,
        TipoSolucion TipoSolucion,
        TipoActualizacionDatos TipoActualizacion,
        EstadoReporteProveedorTicket Proveedor,
        bool VaVP,
        bool TieneCausaRaiz,
        bool TieneJira);

    public async Task<IndicadoresGestionDto> ObtenerAsync(
        DateTime? desde, DateTime? hasta, CancellationToken ct = default)
    {
        var q = _db.TicketSeguimientos.AsNoTracking().AsQueryable();

        // Rango sobre la fecha de creación del seguimiento. Tope EXCLUSIVO:
        // así el último día del rango entra completo.
        if (desde.HasValue) q = q.Where(s => s.CreadoEn >= desde.Value.Date);
        if (hasta.HasValue) q = q.Where(s => s.CreadoEn < hasta.Value.Date.AddDays(1));

        var datos = await q.Select(s => new Fila(
                s.Sistema,
                s.TipoSolucion,
                s.TipoActualizacionDatos,
                s.ReportadoProveedor,
                s.VaReporteVP,
                s.CausaRaiz != null && s.CausaRaiz != "",
                s.Jiras.Any()))
            .ToListAsync(ct);

        var total = datos.Count;
        var dto = new IndicadoresGestionDto { TotalGestionados = total };

        dto.PorSistema = Contar(
            datos.Select(d => d.Sistema).Where(s => s != SistemaAfectado.NoEspecificado),
            s => (int)s, SistemaAfectadoInfo.Nombre, total);

        dto.PorTipoSolucion = Contar(
            datos.Select(d => d.TipoSolucion).Where(t => t != TipoSolucion.NoEspecificado),
            t => (int)t, TipoSolucionInfo.Nombre, total);

        // Caso especial (punto 7): desglose de las actualizaciones de datos.
        dto.PorTipoActualizacionDatos = Contar(
            datos.Select(d => d.TipoActualizacion).Where(t => t != TipoActualizacionDatos.NoAplica),
            t => (int)t, TipoActualizacionDatosInfo.Nombre, total);

        dto.TotalActualizacionesDatos = datos.Count(d => d.TipoSolucion == TipoSolucion.ActualizacionDatos);

        dto.ReportadosProveedor = datos.Count(d => d.Proveedor == EstadoReporteProveedorTicket.Si);
        dto.PendientesReportarProveedor = datos.Count(d => d.Proveedor == EstadoReporteProveedorTicket.Pendiente);
        dto.NoRequierenProveedor = datos.Count(d => d.Proveedor == EstadoReporteProveedorTicket.No);

        dto.ConCausaRaiz = datos.Count(d => d.TieneCausaRaiz);
        dto.SinCausaRaiz = total - dto.ConCausaRaiz;
        dto.ConJira = datos.Count(d => d.TieneJira);
        dto.ReportadosVP = datos.Count(d => d.VaVP);

        dto.TopSistemas = dto.PorSistema.Take(6).ToList();
        dto.TopTiposSolucion = dto.PorTipoSolucion.Take(6).ToList();

        return dto;
    }

    /// <summary>Agrupa una secuencia de enums en conteos ordenados con porcentaje.</summary>
    private static List<ConteoClasificacionDto> Contar<T>(
        IEnumerable<T> valores, Func<T, int> aEntero, Func<T, string> aNombre, int total) where T : notnull
        => valores
            .GroupBy(v => v)
            .Select(g => new ConteoClasificacionDto
            {
                Valor = aEntero(g.Key),
                Nombre = aNombre(g.Key),
                Cantidad = g.Count(),
                Porcentaje = total == 0 ? 0 : Math.Round(g.Count() * 100.0 / total, 1)
            })
            .OrderByDescending(x => x.Cantidad)
            .ThenBy(x => x.Nombre)
            .ToList();
}
