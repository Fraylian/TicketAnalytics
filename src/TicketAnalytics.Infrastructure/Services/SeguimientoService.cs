using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Common.Models;
using TicketAnalytics.Application.Seguimiento.DTOs;
using TicketAnalytics.Application.Tickets.DTOs;
using TicketAnalytics.Domain.Entities;
using TicketAnalytics.Domain.Enums;
using TicketAnalytics.Infrastructure.Persistence.Analytics;

namespace TicketAnalytics.Infrastructure.Services;

/// <summary>Seguimiento operativo con historial cronologico auditable.</summary>
public class SeguimientoService : ISeguimientoService
{
    private readonly AnalyticsContext _db;
    public SeguimientoService(AnalyticsContext db) => _db = db;

    public static string EtapaTexto(EtapaSeguimiento e) => e switch
    {
        EtapaSeguimiento.PendienteAnalisis => "Pendiente de análisis",
        EtapaSeguimiento.EnAnalisis => "En análisis",
        EtapaSeguimiento.EnDesarrollo => "En desarrollo",
        EtapaSeguimiento.EnEsperaProveedor => "En espera del proveedor",
        EtapaSeguimiento.ReportadoProveedor => "Reportado al proveedor",
        EtapaSeguimiento.EnEsperaUsuario => "En espera del usuario",
        EtapaSeguimiento.EnPruebasInternas => "En pruebas internas",
        EtapaSeguimiento.EnQA => "En QA",
        EtapaSeguimiento.PendientePase => "Pendiente de pase",
        EtapaSeguimiento.PendienteOrdenCambio => "Pendiente de orden de cambio",
        EtapaSeguimiento.EnImplementacion => "En proceso de implementación",
        EtapaSeguimiento.EnProduccion => "En producción",
        EtapaSeguimiento.Cerrado => "Cerrado",
        EtapaSeguimiento.Cancelado => "Cancelado",
        EtapaSeguimiento.Reabierto => "Reabierto",
        _ => e.ToString()
    };

    public async Task<SeguimientoDto?> ObtenerPorRefNumAsync(string refNum, CancellationToken ct = default)
    {
        var s = await _db.TicketSeguimientos.AsNoTracking()
            .Include(x => x.Actualizaciones).Include(x => x.Jiras)
            .Include(x => x.ReportesProveedor).ThenInclude(r => r.Proveedor)
            .Include(x => x.Evidencias)
            .FirstOrDefaultAsync(x => x.RefNum == refNum, ct);
        return s is null ? null : Map(s);
    }

    /// <summary>
    /// Finaliza la gestión de un caso: deja de estar activo y queda registrado el
    /// motivo del cierre como última entrada del historial. No se borra nada.
    /// </summary>
    public async Task FinalizarAsync(long seguimientoId, string motivo, string? usuario, CancellationToken ct = default)
    {
        var s = await _db.TicketSeguimientos.FirstOrDefaultAsync(x => x.SeguimientoId == seguimientoId, ct)
                ?? throw new KeyNotFoundException("El seguimiento no existe.");

        var etapaAnterior = s.EtapaActual;
        s.Activo = false;
        s.EtapaActual = EtapaSeguimiento.Cerrado;
        s.ActualizadoEn = DateTime.UtcNow;

        _db.SeguimientoActualizaciones.Add(new SeguimientoActualizacion
        {
            SeguimientoId = s.SeguimientoId,
            TipoActualizacion = "Cierre",
            Contenido = string.IsNullOrWhiteSpace(motivo) ? "Gestión finalizada." : motivo.Trim(),
            EtapaAnterior = etapaAnterior,
            EtapaNueva = EtapaSeguimiento.Cerrado,
            Autor = usuario ?? "sistema",
            CreadoEn = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Reabre una gestión finalizada (vuelve a la bandeja de activas).</summary>
    public async Task ReabrirAsync(long seguimientoId, string? usuario, CancellationToken ct = default)
    {
        var s = await _db.TicketSeguimientos.FirstOrDefaultAsync(x => x.SeguimientoId == seguimientoId, ct)
                ?? throw new KeyNotFoundException("El seguimiento no existe.");

        s.Activo = true;
        s.EtapaActual = EtapaSeguimiento.Reabierto;
        s.ActualizadoEn = DateTime.UtcNow;

        _db.SeguimientoActualizaciones.Add(new SeguimientoActualizacion
        {
            SeguimientoId = s.SeguimientoId,
            TipoActualizacion = "Reapertura",
            Contenido = "Gestión reabierta.",
            EtapaAnterior = EtapaSeguimiento.Cerrado,
            EtapaNueva = EtapaSeguimiento.Reabierto,
            Autor = usuario ?? "sistema",
            CreadoEn = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<SeguimientoDto> CrearAsync(CrearSeguimientoRequest req, CancellationToken ct = default)
    {
        var existe = await _db.TicketSeguimientos.FirstOrDefaultAsync(x => x.RefNum == req.RefNum, ct);
        if (existe != null) return Map(existe);

        var esOc = string.Equals(req.TipoCaso, "Orden de Cambio", StringComparison.OrdinalIgnoreCase);
        var s = new TicketSeguimiento
        {
            RefNum = req.RefNum,
            TipoOrigen = esOc ? "chg" : "cr",
            CrId = req.CrId, Categoria = req.Categoria, TipoCaso = req.TipoCaso,
            TecnicoResponsable = req.TecnicoResponsable, EtapaActual = EtapaSeguimiento.PendienteAnalisis,
            // Si es OC, el seguimiento vive bajo esa OC desde el inicio
            OrdenCambioRef = esOc ? req.RefNum : req.OrdenCambioRef,
            ContinuaBajoOC = esOc || !string.IsNullOrWhiteSpace(req.OrdenCambioRef),
            CreadoPor = req.TecnicoResponsable, CreadoEn = DateTime.UtcNow, ActualizadoEn = DateTime.UtcNow
        };
        _db.TicketSeguimientos.Add(s);

        // Primera entrada del historial
        s.Actualizaciones.Add(new SeguimientoActualizacion
        {
            TipoActualizacion = "Creación",
            Contenido = "Seguimiento iniciado.",
            EtapaNueva = EtapaSeguimiento.PendienteAnalisis,
            Autor = req.TecnicoResponsable ?? "sistema"
        });
        await _db.SaveChangesAsync(ct);
        return Map(s);
    }

    public async Task<ActualizacionDto> AgregarActualizacionAsync(string refNum, AgregarActualizacionRequest req, CancellationToken ct = default)
    {
        var s = await _db.TicketSeguimientos.Include(x => x.Actualizaciones).FirstOrDefaultAsync(x => x.RefNum == refNum, ct)
                ?? throw new InvalidOperationException($"No existe seguimiento para el ticket {refNum}.");

        var etapaAnterior = s.EtapaActual;
        EtapaSeguimiento? etapaNueva = null;
        if (req.NuevaEtapa.HasValue && Enum.IsDefined(typeof(EtapaSeguimiento), req.NuevaEtapa.Value))
        {
            etapaNueva = (EtapaSeguimiento)req.NuevaEtapa.Value;
            s.EtapaActual = etapaNueva.Value;
            s.Activo = etapaNueva is not (EtapaSeguimiento.Cerrado or EtapaSeguimiento.Cancelado);
        }
        if (req.ProximosPasos != null) s.ProximosPasos = req.ProximosPasos;
        if (req.ProximoSeguimiento != null) s.ProximoSeguimiento = req.ProximoSeguimiento;
        s.ActualizadoEn = DateTime.UtcNow;

        var act = new SeguimientoActualizacion
        {
            SeguimientoId = s.SeguimientoId,
            TipoActualizacion = req.TipoActualizacion,
            Contenido = req.Contenido,
            EtapaAnterior = etapaNueva.HasValue ? etapaAnterior : null,
            EtapaNueva = etapaNueva,
            Autor = req.Autor,
            CreadoEn = DateTime.UtcNow
        };
        _db.SeguimientoActualizaciones.Add(act);
        await _db.SaveChangesAsync(ct);

        return new ActualizacionDto
        {
            ActualizacionId = act.ActualizacionId, TipoActualizacion = act.TipoActualizacion, Contenido = act.Contenido,
            EtapaAnterior = (int?)act.EtapaAnterior, EtapaNueva = (int?)act.EtapaNueva,
            EtapaNuevaTexto = act.EtapaNueva.HasValue ? EtapaTexto(act.EtapaNueva.Value) : null,
            Autor = act.Autor, CreadoEn = act.CreadoEn
        };
    }

    public async Task<IReadOnlyList<ActualizacionDto>> HistorialAsync(string refNum, CancellationToken ct = default)
    {
        var s = await _db.TicketSeguimientos.AsNoTracking().FirstOrDefaultAsync(x => x.RefNum == refNum, ct);
        if (s is null) return Array.Empty<ActualizacionDto>();
        return await _db.SeguimientoActualizaciones.AsNoTracking()
            .Where(a => a.SeguimientoId == s.SeguimientoId)
            .OrderByDescending(a => a.CreadoEn)
            .Select(a => new ActualizacionDto
            {
                ActualizacionId = a.ActualizacionId, TipoActualizacion = a.TipoActualizacion, Contenido = a.Contenido,
                EtapaAnterior = (int?)a.EtapaAnterior, EtapaNueva = (int?)a.EtapaNueva, Autor = a.Autor, CreadoEn = a.CreadoEn
            }).ToListAsync(ct);
    }

    public async Task<PagedResult<SeguimientoDto>> BuscarAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var q = _db.TicketSeguimientos.AsNoTracking()
            .Include(x => x.Actualizaciones).Include(x => x.Jiras)
            .Include(x => x.ReportesProveedor).ThenInclude(r => r.Proveedor)
            .Include(x => x.Evidencias)
            .AsQueryable();

        if (f.EtapaSeguimiento.HasValue) q = q.Where(s => (int)s.EtapaActual == f.EtapaSeguimiento.Value);
        if (f.EnEsperaProveedor == true) q = q.Where(s => s.EtapaActual == EtapaSeguimiento.EnEsperaProveedor);
        if (f.ConJira == true) q = q.Where(s => s.Jiras.Any());
        if (f.Reabierto == true) q = q.Where(s => s.EtapaActual == EtapaSeguimiento.Reabierto);
        if (f.CerradoConSeguimientoPendiente == true) q = q.Where(s => s.ContinuaBajoOC && s.Activo);
        // Bandeja: activas vs finalizadas
        if (f.SoloActivos.HasValue) q = q.Where(s => s.Activo == f.SoloActivos.Value);
        if (f.SinActualizacionReciente == true)
        {
            var limite = DateTime.UtcNow.AddDays(-(f.DiasSinActualizacion ?? 7));
            q = q.Where(s => s.ActualizadoEn < limite);
        }
        if (!string.IsNullOrWhiteSpace(f.Texto)) q = q.Where(s => s.RefNum.Contains(f.Texto));

        var total = await q.CountAsync(ct);
        var page = await q.OrderByDescending(s => s.ActualizadoEn)
            .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct);

        return new PagedResult<SeguimientoDto>
        {
            Items = page.Select(Map).ToList(), TotalCount = total, Page = f.Page, PageSize = f.PageSize
        };
    }

    private static SeguimientoDto Map(TicketSeguimiento s) => new()
    {
        SeguimientoId = s.SeguimientoId, RefNum = s.RefNum, TipoOrigen = s.TipoOrigen,
        CrId = s.CrId, Categoria = s.Categoria, TipoCaso = s.TipoCaso,
        EtapaActual = (int)s.EtapaActual, EtapaActualTexto = EtapaTexto(s.EtapaActual),
        TecnicoResponsable = s.TecnicoResponsable, OrdenCambioRef = s.OrdenCambioRef, ContinuaBajoOC = s.ContinuaBajoOC,
        ProximosPasos = s.ProximosPasos, ProximoSeguimiento = s.ProximoSeguimiento, Activo = s.Activo,
        CreadoEn = s.CreadoEn, ActualizadoEn = s.ActualizadoEn,
        CantidadActualizaciones = s.Actualizaciones?.Count ?? 0,
        CantidadJiras = s.Jiras?.Count ?? 0,
        CantidadReportesProveedor = s.ReportesProveedor?.Count ?? 0,
        CantidadEvidencias = s.Evidencias?.Count ?? 0,

        // Clasificación estructurada
        ReportadoProveedor = (int)s.ReportadoProveedor,
        FechaReporteProveedor = s.FechaReporteProveedor,
        TicketProveedor = s.TicketProveedor,
        Sistema = (int)s.Sistema,
        SistemaTexto = SistemaAfectadoInfo.Nombre(s.Sistema),
        TipoSolucion = (int)s.TipoSolucion,
        TipoSolucionTexto = TipoSolucionInfo.Nombre(s.TipoSolucion),
        TipoActualizacionDatos = (int)s.TipoActualizacionDatos,
        TipoActualizacionDatosTexto = TipoActualizacionDatosInfo.Nombre(s.TipoActualizacionDatos),
        AreaDestino = s.AreaDestino,
        CausaRaiz = s.CausaRaiz,
        TicketCausaRaiz = s.TicketCausaRaiz,

        // Reporte VP
        VaReporteVP = s.VaReporteVP,
        PeriodoReporteVP = s.PeriodoReporteVP,
        CategoriaReporteVPId = s.CategoriaReporteVPId,
        EstadoReporteVP = (int)s.EstadoReporteVP,
        NotaReporteVP = s.NotaReporteVP,

        // Lo ya registrado: permite al formulario avisar en vez de duplicar
        ReportesProveedor = (s.ReportesProveedor ?? new()).Select(r => new ReporteProveedorResumenDto
        {
            ReporteId = r.ReporteId,
            ProveedorId = r.ProveedorId,
            Proveedor = r.Proveedor?.Nombre ?? "",
            NumeroCasoExterno = r.NumeroCasoExterno,
            Titulo = r.Titulo,
            Urgencia = r.Urgencia,
            Estado = (int)r.Estado,
            EstadoTexto = EstadoProveedorTexto(r.Estado),
            FechaReporte = r.FechaReporte,
            CumplioSla = r.CumplioSla,
            ResultadoSolucion = r.ResultadoSolucion
        }).OrderByDescending(r => r.FechaReporte).ToList(),

        Jiras = (s.Jiras ?? new()).Select(j => new JiraResumenDto
        {
            JiraId = j.JiraId,
            ClaveJira = j.ClaveJira,
            Titulo = j.Titulo,
            Estado = (int)j.Estado,
            EstadoTexto = j.Estado.ToString(),
            Url = j.Url,
            VinculadoEn = j.VinculadoEn
        }).OrderByDescending(j => j.VinculadoEn).ToList()
    };

    private static string EstadoProveedorTexto(EstadoReporteProveedor e) => e switch
    {
        EstadoReporteProveedor.Abierto => "Abierto",
        EstadoReporteProveedor.EnEsperaRespuesta => "En espera de respuesta",
        EstadoReporteProveedor.RespuestaRecibida => "Respuesta recibida",
        EstadoReporteProveedor.EnSeguimiento => "En seguimiento",
        EstadoReporteProveedor.Resuelto => "Resuelto",
        EstadoReporteProveedor.Cerrado => "Cerrado",
        _ => e.ToString()
    };
}
