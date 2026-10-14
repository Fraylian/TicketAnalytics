using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Common.Models;
using TicketAnalytics.Application.Dashboard.DTOs;
using TicketAnalytics.Domain.Entities;
using TicketAnalytics.Domain.Enums;
using TicketAnalytics.Infrastructure.Persistence.Analytics;

namespace TicketAnalytics.Infrastructure.Services;

/// <summary>
/// Indicadores construidos sobre la clasificación que registra el analista (base local):
/// sistema → proveedor, tipo de solución, subtipo de actualización de datos, tipo de
/// corrección, causa raíz y estado de reporte al proveedor.
/// Antes se traían todos los seguimientos del rango a memoria y se agrupaba en C#; ahora
/// cada corte es un GROUP BY en SQL y a memoria solo llegan los conteos.
/// </summary>
public class IndicadoresGestionService : IIndicadoresGestionService
{
    private readonly AnalyticsContext _db;

    public IndicadoresGestionService(AnalyticsContext db) => _db = db;

    /// <summary>Seguimientos del rango, sobre la fecha de creación. Tope EXCLUSIVO: el último día entra completo.</summary>
    private IQueryable<TicketSeguimiento> Rango(DateTime? desde, DateTime? hasta)
    {
        var q = _db.TicketSeguimientos.AsNoTracking().AsQueryable();
        if (desde.HasValue) q = q.Where(s => s.CreadoEn >= desde.Value.Date);
        if (hasta.HasValue) q = q.Where(s => s.CreadoEn < hasta.Value.Date.AddDays(1));
        return q;
    }

    public async Task<IndicadoresGestionDto> ObtenerAsync(DateTime? desde, DateTime? hasta, CancellationToken ct = default)
    {
        var q = Rango(desde, hasta);

        // 1) Totales: una sola consulta con conteos condicionales.
        var tot = await q.GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(),
            Reportados = g.Count(s => s.ReportadoProveedor == EstadoReporteProveedorTicket.Si),
            Pendientes = g.Count(s => s.ReportadoProveedor == EstadoReporteProveedorTicket.Pendiente),
            NoRequieren = g.Count(s => s.ReportadoProveedor == EstadoReporteProveedorTicket.No),
            ConCausa = g.Count(s => s.CausaRaiz != null && s.CausaRaiz != ""),
            VP = g.Count(s => s.VaReporteVP),
            ActDatos = g.Count(s => s.TipoSolucion == TipoSolucion.ActualizacionDatos),
            SinSistema = g.Count(s => s.SistemaId == null)
        }).FirstOrDefaultAsync(ct);

        var total = tot?.Total ?? 0;
        var dto = new IndicadoresGestionDto
        {
            TotalGestionados = total,
            ReportadosProveedor = tot?.Reportados ?? 0,
            PendientesReportarProveedor = tot?.Pendientes ?? 0,
            NoRequierenProveedor = tot?.NoRequieren ?? 0,
            ConCausaRaiz = tot?.ConCausa ?? 0,
            SinCausaRaiz = total - (tot?.ConCausa ?? 0),
            ReportadosVP = tot?.VP ?? 0,
            TotalActualizacionesDatos = tot?.ActDatos ?? 0,
            SinSistema = tot?.SinSistema ?? 0
        };
        if (total == 0) return dto;

        dto.ConJira = await q.CountAsync(s => s.Jiras.Any(), ct);

        // 2) Sistema × tipo de solución (GROUP BY en SQL). El catálogo de sistemas es una
        //    tabla pequeña: se lee aparte para poner nombre, proveedor y color.
        var sisSol = await q.Where(s => s.SistemaId != null)
            .GroupBy(s => new { s.SistemaId, s.TipoSolucion })
            .Select(g => new { g.Key.SistemaId, g.Key.TipoSolucion, N = g.Count() })
            .ToListAsync(ct);

        var catalogo = await _db.Sistemas.AsNoTracking()
            .Select(s => new { s.SistemaId, s.Nombre, s.Color, s.Orden, Proveedor = s.Proveedor != null ? s.Proveedor.Nombre : null })
            .ToDictionaryAsync(s => s.SistemaId, ct);

        dto.SistemasConSolucion = sisSol
            .GroupBy(x => x.SistemaId!.Value)
            .Select(g =>
            {
                catalogo.TryGetValue(g.Key, out var c);
                var conSol = g.Where(x => x.TipoSolucion != TipoSolucion.NoEspecificado).ToList();
                var totSis = g.Sum(x => x.N);
                return new SistemaSolucionDto
                {
                    SistemaId = g.Key,
                    Sistema = c?.Nombre ?? $"Sistema {g.Key}",
                    Proveedor = c?.Proveedor,
                    Color = c?.Color,
                    Total = totSis,
                    SinSolucion = totSis - conSol.Sum(x => x.N),
                    Soluciones = conSol
                        .Select(x => new ConteoClasificacionDto
                        {
                            Valor = (int)x.TipoSolucion,
                            Nombre = TipoSolucionInfo.Nombre(x.TipoSolucion),
                            Cantidad = x.N,
                            Porcentaje = Pct(x.N, totSis)
                        })
                        .OrderByDescending(x => x.Cantidad).ThenBy(x => x.Nombre).ToList()
                };
            })
            .OrderByDescending(x => x.Total).ThenBy(x => x.Sistema)
            .ToList();

        dto.PorSistema = dto.SistemasConSolucion
            .Select(x => new ConteoClasificacionDto
            {
                Valor = x.SistemaId, Nombre = x.Sistema, Cantidad = x.Total,
                Porcentaje = Pct(x.Total, total), Color = x.Color, Detalle = x.Proveedor ?? "Proveedor por asignar"
            }).ToList();

        // 3) Tipos de solución y subtipos de actualización de datos.
        var porSol = await q.Where(s => s.TipoSolucion != TipoSolucion.NoEspecificado)
            .GroupBy(s => s.TipoSolucion).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        dto.PorTipoSolucion = Ordenar(porSol.Select(x => Conteo((int)x.Key, TipoSolucionInfo.Nombre(x.Key), x.N, total)));

        var porAct = await q.Where(s => s.TipoActualizacionDatos != TipoActualizacionDatos.NoAplica)
            .GroupBy(s => s.TipoActualizacionDatos).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        dto.PorTipoActualizacionDatos = Ordenar(porAct.Select(x =>
            Conteo((int)x.Key, TipoActualizacionDatosInfo.Nombre(x.Key), x.N, dto.TotalActualizacionesDatos)));

        // 4) Tipo de corrección (catálogo local).
        var porCor = await q.Where(s => s.TipoCorreccionId != null)
            .GroupBy(s => new { s.TipoCorreccionId, s.TipoCorreccion!.Nombre })
            .Select(g => new { Id = g.Key.TipoCorreccionId!.Value, g.Key.Nombre, N = g.Count() }).ToListAsync(ct);
        dto.PorTipoCorreccion = Ordenar(porCor.Select(x => Conteo(x.Id, x.Nombre, x.N, total)));

        // 5) Causas raíz más repetidas (texto libre: se agrupa por el texto recortado).
        var porCausa = await q.Where(s => s.CausaRaiz != null && s.CausaRaiz != "")
            .GroupBy(s => s.CausaRaiz!.Trim())
            .Select(g => new { Causa = g.Key, N = g.Count() })
            .OrderByDescending(x => x.N).Take(10).ToListAsync(ct);
        dto.PorCausaRaiz = porCausa.Select((x, i) => Conteo(i + 1, x.Causa, x.N, total)).ToList();

        dto.TopSistemas = dto.PorSistema.Take(6).ToList();
        dto.TopTiposSolucion = dto.PorTipoSolucion.Take(6).ToList();
        return dto;
    }

    public async Task<PagedResult<CasoProveedorDto>> PendientesReportarAsync(
        DateTime? desde, DateTime? hasta, int page, int pageSize, CancellationToken ct = default)
    {
        var q = Rango(desde, hasta).Where(s => s.ReportadoProveedor == EstadoReporteProveedorTicket.Pendiente);
        var total = await q.CountAsync(ct);

        var pagina = page < 1 ? 1 : page;
        var tam = pageSize is < 1 or > 100 ? 10 : pageSize;
        var filas = await q.OrderBy(s => s.CreadoEn)
            .Skip((pagina - 1) * tam).Take(tam)
            .Select(s => new
            {
                s.SeguimientoId, s.RefNum, s.TipoOrigen, s.TecnicoResponsable, s.EtapaActual, s.TicketProveedor, s.CreadoEn,
                Sistema = s.Sistema != null ? s.Sistema.Nombre : null,
                Proveedor = s.Sistema != null && s.Sistema.Proveedor != null ? s.Sistema.Proveedor.Nombre : null
            })
            .ToListAsync(ct);

        var hoy = DateTime.UtcNow;
        return new PagedResult<CasoProveedorDto>
        {
            Items = filas.Select(f => new CasoProveedorDto
            {
                SeguimientoId = f.SeguimientoId, RefNum = f.RefNum, TipoOrigen = f.TipoOrigen,
                Sistema = f.Sistema, Proveedor = f.Proveedor, Tecnico = f.TecnicoResponsable,
                Etapa = SeguimientoService.EtapaTexto(f.EtapaActual), TicketProveedor = f.TicketProveedor,
                Desde = f.CreadoEn, Dias = Math.Max(0, (int)(hoy - f.CreadoEn).TotalDays)
            }).ToList(),
            TotalCount = total, Page = pagina, PageSize = tam
        };
    }

    public async Task<IReadOnlyList<CasoProveedorDto>> EnEsperaProveedorAsync(
        string? analistaUuidHex, string? analistaNombre, CancellationToken ct = default)
    {
        var uuid = HexABytes(analistaUuidHex);
        var nombre = analistaNombre?.Trim();
        if (uuid is null && string.IsNullOrEmpty(nombre)) return Array.Empty<CasoProveedorDto>();

        // Mismo criterio que la tarjeta "Espera proveedor" de /analistas: por uuid, o por
        // nombre cuando el seguimiento no tiene uuid.
        var q = _db.TicketSeguimientos.AsNoTracking()
            .Where(s => s.EtapaActual == EtapaSeguimiento.EnEsperaProveedor)
            .Where(s => (uuid != null && s.TecnicoUuid == uuid)
                     || (s.TecnicoUuid == null && nombre != null && s.TecnicoResponsable == nombre));

        var filas = await q.Select(s => new
            {
                s.SeguimientoId, s.RefNum, s.TipoOrigen, s.TecnicoResponsable, s.EtapaActual, s.TicketProveedor, s.ActualizadoEn,
                Sistema = s.Sistema != null ? s.Sistema.Nombre : null,
                ProveedorSistema = s.Sistema != null && s.Sistema.Proveedor != null ? s.Sistema.Proveedor.Nombre : null,
                // Si el caso ya se reportó a un proveedor concreto, ese manda sobre el del sistema.
                ProveedorReporte = s.ReportesProveedor.OrderByDescending(r => r.FechaReporte).Select(r => r.Proveedor!.Nombre).FirstOrDefault(),
                // Desde cuándo espera: la última vez que el caso entró en esta etapa.
                InicioEspera = s.Actualizaciones.Where(a => a.EtapaNueva == EtapaSeguimiento.EnEsperaProveedor
                                                            && a.EtapaAnterior != EtapaSeguimiento.EnEsperaProveedor)
                                                .Max(a => (DateTime?)a.CreadoEn)
            })
            .ToListAsync(ct);

        var hoy = DateTime.UtcNow;
        return filas.Select(f =>
            {
                var desde = f.InicioEspera ?? f.ActualizadoEn;
                return new CasoProveedorDto
                {
                    SeguimientoId = f.SeguimientoId, RefNum = f.RefNum, TipoOrigen = f.TipoOrigen,
                    Sistema = f.Sistema, Proveedor = f.ProveedorReporte ?? f.ProveedorSistema,
                    Tecnico = f.TecnicoResponsable, Etapa = SeguimientoService.EtapaTexto(f.EtapaActual),
                    TicketProveedor = f.TicketProveedor, Desde = desde, Dias = Math.Max(0, (int)(hoy - desde).TotalDays)
                };
            })
            .OrderByDescending(x => x.Dias).ToList();
    }

    public async Task<IReadOnlyList<CasoClasificadoDto>> CasosClasificadosAsync(
        DateTime? desde, DateTime? hasta, int? sistemaId, CancellationToken ct = default)
    {
        var q = Rango(desde, hasta);
        if (sistemaId is > 0) q = q.Where(s => s.SistemaId == sistemaId);

        var filas = await q.OrderBy(s => s.Sistema != null ? s.Sistema.Orden : int.MaxValue).ThenBy(s => s.CreadoEn)
            .Select(s => new
            {
                s.RefNum, s.TipoOrigen, s.TipoSolucion, s.TipoActualizacionDatos, s.EtapaActual,
                s.TecnicoResponsable, s.CausaRaiz, s.ReportadoProveedor, s.CreadoEn,
                Sistema = s.Sistema != null ? s.Sistema.Nombre : null,
                Proveedor = s.Sistema != null && s.Sistema.Proveedor != null ? s.Sistema.Proveedor.Nombre : null,
                Correccion = s.TipoCorreccion != null ? s.TipoCorreccion.Nombre : null
            })
            .ToListAsync(ct);

        return filas.Select(f => new CasoClasificadoDto
        {
            RefNum = f.RefNum, TipoOrigen = f.TipoOrigen, Sistema = f.Sistema, Proveedor = f.Proveedor,
            TipoSolucion = f.TipoSolucion == TipoSolucion.NoEspecificado ? null : TipoSolucionInfo.Nombre(f.TipoSolucion),
            TipoActualizacion = f.TipoActualizacionDatos == TipoActualizacionDatos.NoAplica ? null : TipoActualizacionDatosInfo.Nombre(f.TipoActualizacionDatos),
            TipoCorreccion = f.Correccion, Etapa = SeguimientoService.EtapaTexto(f.EtapaActual),
            Tecnico = f.TecnicoResponsable, CausaRaiz = f.CausaRaiz, CreadoEn = f.CreadoEn,
            ReportadoProveedor = f.ReportadoProveedor switch
            {
                EstadoReporteProveedorTicket.Si => "Sí",
                EstadoReporteProveedorTicket.No => "No",
                _ => "Pendiente"
            }
        }).ToList();
    }

    private static double Pct(int parte, int total) => total == 0 ? 0 : Math.Round(parte * 100.0 / total, 1);

    private static ConteoClasificacionDto Conteo(int valor, string nombre, int n, int total)
        => new() { Valor = valor, Nombre = nombre, Cantidad = n, Porcentaje = Pct(n, total) };

    private static List<ConteoClasificacionDto> Ordenar(IEnumerable<ConteoClasificacionDto> l)
        => l.OrderByDescending(x => x.Cantidad).ThenBy(x => x.Nombre).ToList();

    private static byte[]? HexABytes(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var h = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;
        try { return h.Length == 32 ? Convert.FromHexString(h) : null; }
        catch (FormatException) { return null; }
    }
}
