using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.ReporteVP.DTOs;
using TicketAnalytics.Domain.Common;
using TicketAnalytics.Domain.Entities;
using TicketAnalytics.Domain.Enums;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;

namespace TicketAnalytics.Infrastructure.Services;

/// <summary>
/// Arma el reporte mensual al VP. Los casos viven en la base local (TicketSeguimiento
/// con VaReporteVP = 1 y su período); el resumen/título se completa leyendo MDB por
/// ref_num en una consulta aparte (nunca un JOIN cross-DB).
/// </summary>
public class ReporteVPService : IReporteVPService
{
    private readonly AnalyticsContext _analytics;
    private readonly SdmReadContext _sdm;
    private readonly ILogger<ReporteVPService> _logger;

    public ReporteVPService(AnalyticsContext analytics, SdmReadContext sdm, ILogger<ReporteVPService> logger)
    {
        _analytics = analytics;
        _sdm = sdm;
        _logger = logger;
    }

    // ===================== Consulta base =====================

    /// <summary>Proyección plana del seguimiento: NO carga las colecciones hijas.</summary>
    private IQueryable<CasoReporteVPDto> ConsultaBase(ReporteVPFiltro f, DateOnly periodo)
    {
        var q = from s in _analytics.TicketSeguimientos.AsNoTracking()
                join cat in _analytics.CategoriasReporteVP.AsNoTracking()
                    on s.CategoriaReporteVPId equals cat.CategoriaId into gj
                from cat in gj.DefaultIfEmpty()
                where s.VaReporteVP && s.PeriodoReporteVP == periodo
                select new CasoReporteVPDto
                {
                    SeguimientoId = s.SeguimientoId,
                    RefNum = s.RefNum,
                    TipoOrigen = s.TipoOrigen,
                    CategoriaOrigen = s.Categoria,
                    CategoriaVPId = s.CategoriaReporteVPId,
                    CategoriaVP = cat != null ? cat.Nombre : (s.CategoriaReporteVP ?? "Sin categoría"),
                    CategoriaColor = cat != null ? cat.Color : null,
                    CategoriaOrden = cat != null ? cat.Orden : 999,
                    Estado = s.EstadoReporteVP,
                    Responsable = s.TecnicoResponsable,
                    ReportadoProveedor = s.ReportadoProveedor,
                    NotaEjecutiva = s.NotaReporteVP,
                    UltimaActualizacion = s.ActualizadoEn,
                    Periodo = s.PeriodoReporteVP
                };

        if (f.CategoriaId.HasValue) q = q.Where(c => c.CategoriaVPId == f.CategoriaId);
        if (f.Estado.HasValue && EstadoReporteVPInfo.EsValido(f.Estado.Value))
        {
            var e = (EstadoReporteVP)f.Estado.Value;
            q = q.Where(c => c.Estado == e);
        }
        if (!string.IsNullOrWhiteSpace(f.Responsable))
            q = q.Where(c => c.Responsable == f.Responsable);
        if (f.SoloConProveedor == true)
            q = q.Where(c => c.ReportadoProveedor == EstadoReporteProveedorTicket.Si);
        if (!string.IsNullOrWhiteSpace(f.Texto))
        {
            var t = f.Texto.Trim();
            q = q.Where(c => c.RefNum.Contains(t)
                          || (c.NotaEjecutiva != null && c.NotaEjecutiva.Contains(t))
                          || (c.CategoriaOrigen != null && c.CategoriaOrigen.Contains(t)));
        }
        return q;
    }

    public async Task<List<CasoReporteVPDto>> CasosAsync(ReporteVPFiltro f, CancellationToken ct = default)
    {
        var periodo = f.PeriodoInicio();
        var casos = await ConsultaBase(f, periodo).ToListAsync(ct);
        await CompletarAsync(casos, ct);
        return Ordenar(casos);
    }

    /// <summary>Rellena lo que no vive en Analytics: resumen desde MDB, JIRA y proveedor.</summary>
    private async Task CompletarAsync(List<CasoReporteVPDto> casos, CancellationToken ct)
    {
        if (casos.Count == 0) return;

        foreach (var c in casos)
        {
            c.EstadoTexto = EstadoReporteVPInfo.Nombre(c.Estado);
            c.EstadoColor = EstadoReporteVPInfo.Color(c.Estado);
            c.EstadoGrupo = EstadoReporteVPInfo.Grupo(c.Estado);
            c.ReportadoProveedorTexto = c.ReportadoProveedor switch
            {
                EstadoReporteProveedorTicket.Si => "Sí",
                EstadoReporteProveedorTicket.No => "No",
                _ => "Pendiente"
            };
        }

        // Resumen desde MDB (solo lectura) por ref_num, sin JOIN cross-DB.
        var refsCr = casos.Where(c => c.TipoOrigen != "chg").Select(c => c.RefNum).Distinct().ToList();
        var refsChg = casos.Where(c => c.TipoOrigen == "chg").Select(c => c.RefNum).Distinct().ToList();

        if (refsCr.Count > 0)
        {
            var mapa = await _sdm.CallReqs.AsNoTracking()
                .Where(cr => refsCr.Contains(cr.ref_num))
                .Select(cr => new { cr.ref_num, cr.summary, cr.description })
                .ToDictionaryAsync(x => x.ref_num, x => x.summary ?? x.description, ct);
            foreach (var c in casos.Where(c => c.TipoOrigen != "chg"))
                if (mapa.TryGetValue(c.RefNum, out var texto)) c.Resumen = Limpiar(texto);
        }

        if (refsChg.Count > 0)
        {
            var mapa = await _sdm.Changes.AsNoTracking()
                .Where(ch => refsChg.Contains(ch.chg_ref_num))
                .Select(ch => new { ch.chg_ref_num, ch.summary, ch.description })
                .ToDictionaryAsync(x => x.chg_ref_num, x => x.summary ?? x.description, ct);
            foreach (var c in casos.Where(c => c.TipoOrigen == "chg"))
                if (mapa.TryGetValue(c.RefNum, out var texto)) c.Resumen = Limpiar(texto);
        }

        // JIRA y proveedor: contamos/leemos por seguimiento con proyección (sin cargar colecciones).
        var ids = casos.Select(c => c.SeguimientoId).ToList();

        var jiras = await _analytics.JiraRelaciones.AsNoTracking()
            .Where(j => ids.Contains(j.SeguimientoId))
            .Select(j => new { j.SeguimientoId, j.ClaveJira })
            .ToListAsync(ct);
        var mapaJira = jiras.GroupBy(j => j.SeguimientoId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.ClaveJira).Distinct()));

        var provs = await (from r in _analytics.ReportesProveedor.AsNoTracking()
                           join p in _analytics.Proveedores.AsNoTracking() on r.ProveedorId equals p.ProveedorId
                           where r.SeguimientoId != null && ids.Contains(r.SeguimientoId.Value)
                           select new { SeguimientoId = r.SeguimientoId!.Value, p.Nombre })
                          .ToListAsync(ct);
        var mapaProv = provs.GroupBy(p => p.SeguimientoId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.Nombre).Distinct()));

        foreach (var c in casos)
        {
            if (mapaJira.TryGetValue(c.SeguimientoId, out var j)) c.Jira = j;
            if (mapaProv.TryGetValue(c.SeguimientoId, out var p)) c.Proveedor = p;
        }
    }

    private static List<CasoReporteVPDto> Ordenar(List<CasoReporteVPDto> casos) => casos
        .OrderBy(c => c.CategoriaOrden).ThenBy(c => c.CategoriaVP)
        .ThenBy(c => Array.IndexOf(EstadoReporteVPInfo.Orden, c.Estado))
        .ThenBy(c => c.RefNum)
        .ToList();

    // ===================== Overview =====================

    public async Task<ReporteVPOverviewDto> OverviewAsync(ReporteVPFiltro f, CancellationToken ct = default)
    {
        var periodo = f.PeriodoInicio();
        var anterior = periodo.AddMonths(-1);

        var casos = await CasosAsync(f, ct);

        // Período anterior: solo conteos para la comparación (sin enriquecer).
        var filtroPrev = new ReporteVPFiltro
        {
            Periodo = $"{anterior.Year:0000}-{anterior.Month:00}",
            CategoriaId = f.CategoriaId, Estado = f.Estado,
            Responsable = f.Responsable, SoloConProveedor = f.SoloConProveedor
        };
        var previos = await ConsultaBase(filtroPrev, anterior)
            .Select(c => new { c.Estado, c.ReportadoProveedor }).ToListAsync(ct);

        int Grupo(IEnumerable<EstadoReporteVP> src, string grupo) => src.Count(e => EstadoReporteVPInfo.Grupo(e) == grupo);
        var estadosAct = casos.Select(c => c.Estado).ToList();
        var estadosPrev = previos.Select(p => p.Estado).ToList();

        var dto = new ReporteVPOverviewDto
        {
            Periodo = $"{periodo.Year:0000}-{periodo.Month:00}",
            PeriodoTexto = TextoPeriodo(periodo),
            PeriodoAnteriorTexto = TextoPeriodo(anterior),
            TotalCasos = casos.Count,
            Casos = casos,
            Kpis = new List<KpiVPDto>
            {
                new() { Clave = "total", Titulo = "Total casos", Valor = casos.Count, ValorAnterior = previos.Count },
                new() { Clave = "completados", Titulo = "Completados",
                        Valor = Grupo(estadosAct, "Completado"), ValorAnterior = Grupo(estadosPrev, "Completado") },
                new() { Clave = "proceso", Titulo = "En proceso",
                        Valor = Grupo(estadosAct, "En proceso"), ValorAnterior = Grupo(estadosPrev, "En proceso") },
                new() { Clave = "pendientes", Titulo = "Pendientes",
                        Valor = Grupo(estadosAct, "Pendiente"), ValorAnterior = Grupo(estadosPrev, "Pendiente") },
                new() { Clave = "proveedor", Titulo = "Con proveedor",
                        Valor = casos.Count(c => c.ReportadoProveedor == EstadoReporteProveedorTicket.Si),
                        ValorAnterior = previos.Count(p => p.ReportadoProveedor == EstadoReporteProveedorTicket.Si) },
                new() { Clave = "atencion", Titulo = "Requieren atención",
                        Valor = casos.Count(c => c.Estado == EstadoReporteVP.Bloqueado
                                              || c.ReportadoProveedor == EstadoReporteProveedorTicket.Pendiente),
                        ValorAnterior = previos.Count(p => p.Estado == EstadoReporteVP.Bloqueado
                                              || p.ReportadoProveedor == EstadoReporteProveedorTicket.Pendiente) }
            }
        };

        // Distribución por estado (donut)
        dto.PorEstado = EstadoReporteVPInfo.Orden
            .Select(e => new ConteoEstadoVPDto
            {
                Estado = (int)e,
                Nombre = EstadoReporteVPInfo.Nombre(e),
                Color = EstadoReporteVPInfo.Color(e),
                Cantidad = casos.Count(c => c.Estado == e),
                Porcentaje = casos.Count == 0 ? 0 : Math.Round(casos.Count(c => c.Estado == e) * 100.0 / casos.Count, 1)
            })
            .Where(x => x.Cantidad > 0)
            .ToList();

        // Categorías con desglose por estado (barra apilada + bloques ejecutivos)
        dto.Categorias = casos
            .GroupBy(c => new { c.CategoriaVPId, c.CategoriaVP, c.CategoriaColor, c.CategoriaOrden })
            .Select(g => new CategoriaVPResumenDto
            {
                CategoriaId = g.Key.CategoriaVPId,
                Categoria = g.Key.CategoriaVP,
                Color = g.Key.CategoriaColor,
                Orden = g.Key.CategoriaOrden,
                Total = g.Count(),
                PorEstado = EstadoReporteVPInfo.Orden
                    .Where(e => g.Any(c => c.Estado == e))
                    .ToDictionary(e => EstadoReporteVPInfo.Nombre(e), e => g.Count(c => c.Estado == e)),
                Casos = g.OrderBy(c => Array.IndexOf(EstadoReporteVPInfo.Orden, c.Estado)).ThenBy(c => c.RefNum).ToList()
            })
            .OrderBy(c => c.Orden).ThenByDescending(c => c.Total)
            .ToList();

        dto.Tendencia = await TendenciaAsync(periodo, ct);
        dto.ResumenEjecutivo = Narrativa(dto);
        return dto;
    }

    /// <summary>Últimos 6 meses: casos incluidos y completados.</summary>
    private async Task<List<PuntoTendenciaVPDto>> TendenciaAsync(DateOnly periodo, CancellationToken ct)
    {
        var desde = periodo.AddMonths(-5);
        var datos = await _analytics.TicketSeguimientos.AsNoTracking()
            .Where(s => s.VaReporteVP && s.PeriodoReporteVP != null
                     && s.PeriodoReporteVP >= desde && s.PeriodoReporteVP <= periodo)
            .Select(s => new { s.PeriodoReporteVP, s.EstadoReporteVP })
            .ToListAsync(ct);

        var puntos = new List<PuntoTendenciaVPDto>();
        for (int i = 0; i < 6; i++)
        {
            var m = desde.AddMonths(i);
            var delMes = datos.Where(d => d.PeriodoReporteVP == m).ToList();
            puntos.Add(new PuntoTendenciaVPDto
            {
                Periodo = $"{m.Year:0000}-{m.Month:00}",
                Etiqueta = $"{MesCorto(m.Month)} {m.Year}",
                Total = delMes.Count,
                Completados = delMes.Count(d => d.EstadoReporteVP == EstadoReporteVP.Completado)
            });
        }
        return puntos;
    }

    /// <summary>Resumen narrativo por reglas (sin IA): se actualiza con los filtros.</summary>
    private static string Narrativa(ReporteVPOverviewDto d)
    {
        if (d.TotalCasos == 0)
            return $"No hay casos registrados para {d.PeriodoTexto}.";

        int comp = d.Kpis.FirstOrDefault(k => k.Clave == "completados")?.Valor ?? 0;
        int proc = d.Kpis.FirstOrDefault(k => k.Clave == "proceso")?.Valor ?? 0;
        int pend = d.Kpis.FirstOrDefault(k => k.Clave == "pendientes")?.Valor ?? 0;

        var texto = $"Durante {d.PeriodoTexto} se gestionaron {d.TotalCasos} " +
                    $"{(d.TotalCasos == 1 ? "caso" : "casos")}. " +
                    $"{comp} {(comp == 1 ? "se encuentra completado" : "se encuentran completados")}, " +
                    $"{proc} {(proc == 1 ? "permanece" : "permanecen")} en proceso y " +
                    $"{pend} {(pend == 1 ? "requiere" : "requieren")} seguimiento.";

        var top = d.Categorias.OrderByDescending(c => c.Total).FirstOrDefault();
        if (top is not null && d.TotalCasos > 0)
        {
            var pct = Math.Round(top.Total * 100.0 / d.TotalCasos);
            texto += $" {top.Categoria} concentra el {pct:0}% de los casos del período.";
        }

        var kpiTotal = d.Kpis.FirstOrDefault(k => k.Clave == "total");
        if (kpiTotal?.Delta is int delta && delta != 0 && d.PeriodoAnteriorTexto is not null)
        {
            var signo = delta > 0 ? "más" : "menos";
            texto += $" Son {Math.Abs(delta)} {signo} que en {d.PeriodoAnteriorTexto}.";
        }

        var conProveedor = d.Kpis.FirstOrDefault(k => k.Clave == "proveedor")?.Valor ?? 0;
        if (conProveedor > 0)
            texto += $" {conProveedor} {(conProveedor == 1 ? "caso está reportado" : "casos están reportados")} a proveedor.";

        return texto;
    }

    // ===================== Escritura =====================

    public async Task<CasoReporteVPDto> GuardarCasoAsync(GuardarCasoVPRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.RefNum))
            throw new ArgumentException("El número de caso es obligatorio.");
        if (!EstadoReporteVPInfo.EsValido(req.Estado))
            throw new ArgumentException($"Estado de reporte VP no válido: {req.Estado}.");

        var tipo = req.TipoOrigen == "chg" ? "chg" : "cr";
        var periodo = ParsePeriodo(req.Periodo);

        if (req.CategoriaId.HasValue &&
            !await _analytics.CategoriasReporteVP.AnyAsync(c => c.CategoriaId == req.CategoriaId, ct))
            throw new ArgumentException("La categoría indicada no existe.");

        // Unicidad real del seguimiento: RefNum + TipoOrigen (no solo RefNum).
        var seg = await _analytics.TicketSeguimientos
            .FirstOrDefaultAsync(s => s.RefNum == req.RefNum && s.TipoOrigen == tipo, ct);

        if (seg is null)
        {
            seg = new TicketSeguimiento
            {
                RefNum = req.RefNum.Trim(),
                TipoOrigen = tipo,
                EtapaActual = EtapaSeguimiento.PendienteAnalisis,
                CreadoEn = DateTime.UtcNow,
                CreadoPor = req.Usuario
            };
            _analytics.TicketSeguimientos.Add(seg);
        }

        seg.VaReporteVP = true;
        seg.PeriodoReporteVP = periodo;
        seg.CategoriaReporteVPId = req.CategoriaId;
        seg.EstadoReporteVP = (EstadoReporteVP)req.Estado;
        seg.NotaReporteVP = Recortar(req.NotaEjecutiva, 2000);
        if (!string.IsNullOrWhiteSpace(req.Responsable)) seg.TecnicoResponsable = req.Responsable;
        seg.ActualizadoEn = DateTime.UtcNow;

        await _analytics.SaveChangesAsync(ct);
        _logger.LogInformation("Caso {Ref} ({Tipo}) guardado en reporte VP {Periodo}", seg.RefNum, tipo, periodo);

        var filtro = new ReporteVPFiltro { Periodo = $"{periodo.Year:0000}-{periodo.Month:00}" };
        var casos = await CasosAsync(filtro, ct);
        return casos.FirstOrDefault(c => c.SeguimientoId == seg.SeguimientoId)
               ?? new CasoReporteVPDto { SeguimientoId = seg.SeguimientoId, RefNum = seg.RefNum };
    }

    public async Task QuitarCasoAsync(long seguimientoId, CancellationToken ct = default)
    {
        var seg = await _analytics.TicketSeguimientos.FirstOrDefaultAsync(s => s.SeguimientoId == seguimientoId, ct)
                  ?? throw new KeyNotFoundException("El seguimiento no existe.");
        // Solo lo sacamos del reporte: el seguimiento y su historial se conservan.
        seg.VaReporteVP = false;
        seg.ActualizadoEn = DateTime.UtcNow;
        await _analytics.SaveChangesAsync(ct);
    }

    public async Task MoverPeriodoAsync(long seguimientoId, string periodo, CancellationToken ct = default)
    {
        var seg = await _analytics.TicketSeguimientos.FirstOrDefaultAsync(s => s.SeguimientoId == seguimientoId, ct)
                  ?? throw new KeyNotFoundException("El seguimiento no existe.");
        seg.PeriodoReporteVP = ParsePeriodo(periodo);
        seg.ActualizadoEn = DateTime.UtcNow;
        await _analytics.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CategoriaVPDto>> CategoriasAsync(bool soloActivas = true, CancellationToken ct = default)
        => await _analytics.CategoriasReporteVP.AsNoTracking()
            .Where(c => !soloActivas || c.Activo)
            .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
            .Select(c => new CategoriaVPDto
            {
                CategoriaId = c.CategoriaId, Nombre = c.Nombre, Descripcion = c.Descripcion,
                Orden = c.Orden, Activo = c.Activo, Color = c.Color
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> PeriodosDisponiblesAsync(CancellationToken ct = default)
    {
        var fechas = await _analytics.TicketSeguimientos.AsNoTracking()
            .Where(s => s.VaReporteVP && s.PeriodoReporteVP != null)
            .Select(s => s.PeriodoReporteVP!.Value)
            .Distinct().ToListAsync(ct);

        var actual = DateOnly.FromDateTime(DateTime.Today);
        var mesActual = new DateOnly(actual.Year, actual.Month, 1);
        if (!fechas.Contains(mesActual)) fechas.Add(mesActual);

        return fechas.OrderByDescending(d => d).Select(d => $"{d.Year:0000}-{d.Month:00}").ToList();
    }

    // ===================== Utilidades =====================

    private static DateOnly ParsePeriodo(string? periodo)
    {
        if (!string.IsNullOrWhiteSpace(periodo) && DateOnly.TryParse($"{periodo}-01", out var d))
            return new DateOnly(d.Year, d.Month, 1);
        var hoy = DateTime.Today;
        return new DateOnly(hoy.Year, hoy.Month, 1);
    }

    public static string TextoPeriodo(DateOnly d)
    {
        var cultura = new System.Globalization.CultureInfo("es-DO");
        var mes = cultura.DateTimeFormat.GetMonthName(d.Month);
        return $"{char.ToUpper(mes[0])}{mes[1..]} {d.Year}";
    }

    private static string MesCorto(int mes)
    {
        var cultura = new System.Globalization.CultureInfo("es-DO");
        var m = cultura.DateTimeFormat.GetAbbreviatedMonthName(mes).TrimEnd('.');
        return char.ToUpper(m[0]) + m[1..];
    }

    private static string? Limpiar(string? s)
        => s?.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();

    private static string? Recortar(string? s, int max)
        => string.IsNullOrWhiteSpace(s) ? null : (s.Length > max ? s[..max] : s);
}
