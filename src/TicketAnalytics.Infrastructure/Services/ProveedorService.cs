using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Proveedores.DTOs;
using TicketAnalytics.Domain.Entities;
using TicketAnalytics.Domain.Enums;
using TicketAnalytics.Infrastructure.Persistence.Analytics;

namespace TicketAnalytics.Infrastructure.Services;

public class ProveedorService : IProveedorService
{
    private readonly AnalyticsContext _db;
    public ProveedorService(AnalyticsContext db) => _db = db;

    private static string EstadoTexto(EstadoReporteProveedor e) => e switch
    {
        EstadoReporteProveedor.Abierto => "Abierto",
        EstadoReporteProveedor.EnEsperaRespuesta => "En espera de respuesta",
        EstadoReporteProveedor.RespuestaRecibida => "Respuesta recibida",
        EstadoReporteProveedor.EnSeguimiento => "En seguimiento",
        EstadoReporteProveedor.Resuelto => "Resuelto",
        EstadoReporteProveedor.Cerrado => "Cerrado",
        _ => e.ToString()
    };

    public async Task<IReadOnlyList<ProveedorDto>> ListarAsync(CancellationToken ct = default)
        => await _db.Proveedores.AsNoTracking().Select(p => new ProveedorDto
        {
            ProveedorId = p.ProveedorId, Nombre = p.Nombre, Contacto = p.Contacto, Email = p.Email, Activo = p.Activo,
            ReportesAbiertos = p.Reportes.Count(r => r.Estado != EstadoReporteProveedor.Cerrado && r.Estado != EstadoReporteProveedor.Resuelto)
        }).OrderBy(p => p.Nombre).ToListAsync(ct);

    public async Task<ProveedorDto> CrearAsync(string nombre, string? contacto, string? email, CancellationToken ct = default)
    {
        var p = new Proveedor { Nombre = nombre, Contacto = contacto, Email = email, Activo = true };
        _db.Proveedores.Add(p);
        await _db.SaveChangesAsync(ct);
        return new ProveedorDto { ProveedorId = p.ProveedorId, Nombre = p.Nombre, Contacto = p.Contacto, Email = p.Email, Activo = p.Activo };
    }

    public async Task<ReporteProveedorDto> ReportarAsync(string refNum, ReporteProveedorDto req, CancellationToken ct = default)
    {
        var seg = await _db.TicketSeguimientos.FirstOrDefaultAsync(s => s.RefNum == refNum, ct)
                  ?? throw new InvalidOperationException($"No existe seguimiento para {refNum}. Créalo antes de reportar a proveedor.");

        var r = new ReporteProveedor
        {
            SeguimientoId = seg.SeguimientoId, ProveedorId = req.ProveedorId,
            NumeroCasoExterno = req.NumeroCasoExterno, FechaReporte = req.FechaReporte == default ? DateTime.UtcNow : req.FechaReporte,
            ContextoEnviado = req.ContextoEnviado, Estado = EstadoReporteProveedor.EnEsperaRespuesta,
            ResponsableInterno = req.ResponsableInterno, ProximoSeguimiento = req.ProximoSeguimiento, CreadoEn = DateTime.UtcNow
        };
        _db.ReportesProveedor.Add(r);
        await _db.SaveChangesAsync(ct);
        return await MapAsync(r, ct);
    }

    public async Task<IReadOnlyList<ReporteProveedorDto>> ReportesDeSeguimientoAsync(string refNum, CancellationToken ct = default)
    {
        var seg = await _db.TicketSeguimientos.AsNoTracking().FirstOrDefaultAsync(s => s.RefNum == refNum, ct);
        if (seg is null) return Array.Empty<ReporteProveedorDto>();
        var reportes = await _db.ReportesProveedor.AsNoTracking().Include(r => r.Proveedor)
            .Where(r => r.SeguimientoId == seg.SeguimientoId).ToListAsync(ct);
        return reportes.Select(r => MapSync(r, refNum)).ToList();
    }

    public async Task<IReadOnlyList<ReporteProveedorDto>> ReportesQueNecesitanSeguimientoAsync(CancellationToken ct = default)
    {
        var abiertos = await _db.ReportesProveedor.AsNoTracking().Include(r => r.Proveedor).Include(r => r.Seguimiento)
            .Where(r => r.Estado != EstadoReporteProveedor.Cerrado && r.Estado != EstadoReporteProveedor.Resuelto)
            .ToListAsync(ct);
        return abiertos
            .Select(r => MapSync(r, r.Seguimiento?.RefNum))
            .Where(d => d.NecesitaSeguimiento)
            .OrderByDescending(d => d.DiasSinRespuesta).ToList();
    }

    private async Task<ReporteProveedorDto> MapAsync(ReporteProveedor r, CancellationToken ct)
    {
        var prov = await _db.Proveedores.AsNoTracking().FirstOrDefaultAsync(p => p.ProveedorId == r.ProveedorId, ct);
        var seg = r.SeguimientoId.HasValue
            ? await _db.TicketSeguimientos.AsNoTracking().FirstOrDefaultAsync(s => s.SeguimientoId == r.SeguimientoId.Value, ct)
            : null;
        r.Proveedor = prov;
        return MapSync(r, seg?.RefNum);
    }

    private static ReporteProveedorDto MapSync(ReporteProveedor r, string? refNum)
    {
        var refDate = r.UltimoSeguimiento ?? r.FechaReporte;
        var dias = (int)(DateTime.UtcNow - refDate).TotalDays;
        return new ReporteProveedorDto
        {
            ReporteId = r.ReporteId, SeguimientoId = r.SeguimientoId, RefNum = refNum,
            ProveedorId = r.ProveedorId, ProveedorNombre = r.Proveedor?.Nombre,
            NumeroCasoExterno = r.NumeroCasoExterno, FechaReporte = r.FechaReporte,
            ContextoEnviado = r.ContextoEnviado, RespuestasRecibidas = r.RespuestasRecibidas,
            Estado = (int)r.Estado, EstadoTexto = EstadoTexto(r.Estado),
            UltimoSeguimiento = r.UltimoSeguimiento, ProximoSeguimiento = r.ProximoSeguimiento,
            ResponsableInterno = r.ResponsableInterno, ResultadoSolucion = r.ResultadoSolucion,
            DiasSinRespuesta = dias,
            NecesitaSeguimiento = (r.ProximoSeguimiento != null && r.ProximoSeguimiento <= DateTime.UtcNow)
                                  || dias >= 3
        };
    }

    // ===== Matriz mensual de proveedores =====

    private (DateTime desde, DateTime hasta) RangoMes(ReporteProveedorFiltro f)
    {
        var anio = f.Anio ?? DateTime.Today.Year;
        var mes = f.Mes ?? DateTime.Today.Month;
        var desde = new DateTime(anio, mes, 1);
        return (desde, desde.AddMonths(1));
    }

    private static string SlaHorasTexto(int? horas)
    {
        if (!horas.HasValue) return "—";
        if (horas.Value % 24 == 0 && horas.Value >= 24) return $"{horas.Value / 24} Día(s)";
        return $"{horas.Value} Hora(s)";
    }

    private ReporteProveedorMatrizDto MapMatriz(ReporteProveedor r)
    {
        // Horas reales que tardó (informativo), y SLA de referencia del proveedor.
        double? horas = r.FechaPrimeraRespuesta.HasValue
            ? Math.Round((r.FechaPrimeraRespuesta.Value - r.FechaReporte).TotalHours, 1) : null;
        int? slaRef = r.SlaHorasRespuesta ?? r.Proveedor?.SlaCriticoHoras; // fallback; se recalcula abajo
        if (r.Proveedor != null)
        {
            slaRef = r.Urgencia?.ToLowerInvariant() switch
            {
                "critico" => r.Proveedor.SlaCriticoHoras,
                "alto" => r.Proveedor.SlaAltoHoras,
                "medio" => r.Proveedor.SlaMedioHoras,
                "bajo" => r.Proveedor.SlaBajoHoras,
                _ => r.SlaHorasRespuesta
            } ?? r.SlaHorasRespuesta;
        }

        return new ReporteProveedorMatrizDto
        {
            ReporteId = r.ReporteId, ProveedorId = r.ProveedorId, Proveedor = r.Proveedor?.Nombre ?? "",
            RefNumSistema = r.RefNumSistema, TipoVinculo = r.TipoVinculo, NumeroCasoExterno = r.NumeroCasoExterno,
            Titulo = r.Titulo, Descripcion = r.Descripcion, FechaReporte = r.FechaReporte,
            ContextoEnviado = r.ContextoEnviado, RespuestasRecibidas = r.RespuestasRecibidas,
            Urgencia = r.Urgencia, SlaHorasRespuesta = r.SlaHorasRespuesta, FechaPrimeraRespuesta = r.FechaPrimeraRespuesta,
            BrindoAsistencia = r.BrindoAsistencia, EntregoCambio = r.EntregoCambio, CumplioSla = r.CumplioSla,
            Estado = (int)r.Estado, EstadoTexto = EstadoTexto(r.Estado),
            ResponsableInterno = r.ResponsableInterno, ResultadoSolucion = r.ResultadoSolucion,
            CreadoEn = r.CreadoEn, SlaReferenciaHoras = slaRef, HorasRespuesta = horas
        };
    }

    private async Task<List<ReporteProveedor>> MatrizQueryAsync(ReporteProveedorFiltro f, CancellationToken ct)
    {
        var (desde, hasta) = RangoMes(f);
        var q = _db.ReportesProveedor.AsNoTracking().Include(r => r.Proveedor)
            .Where(r => r.FechaReporte >= desde && r.FechaReporte < hasta);
        if (f.ProveedorId.HasValue) q = q.Where(r => r.ProveedorId == f.ProveedorId.Value);
        if (!string.IsNullOrWhiteSpace(f.ResponsableInterno)) q = q.Where(r => r.ResponsableInterno == f.ResponsableInterno);
        if (f.Estado.HasValue) q = q.Where(r => (int)r.Estado == f.Estado.Value);
        if (!string.IsNullOrWhiteSpace(f.Urgencia)) q = q.Where(r => r.Urgencia == f.Urgencia);
        if (!string.IsNullOrWhiteSpace(f.Texto))
        {
            var t = f.Texto.Trim();
            q = q.Where(r => (r.Titulo != null && r.Titulo.Contains(t)) || (r.Descripcion != null && r.Descripcion.Contains(t))
                          || (r.NumeroCasoExterno != null && r.NumeroCasoExterno.Contains(t))
                          || (r.RefNumSistema != null && r.RefNumSistema.Contains(t)));
        }
        return await q.OrderByDescending(r => r.FechaReporte).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReporteProveedorMatrizDto>> MatrizBuscarAsync(ReporteProveedorFiltro f, CancellationToken ct = default)
    {
        var lista = await MatrizQueryAsync(f, ct);
        var dtos = lista.Select(MapMatriz).ToList();
        // "Incumplido" ahora es lo que el usuario marcó como NO cumplido.
        if (f.SoloIncumplidosSla == true) dtos = dtos.Where(d => d.CumplioSla == false).ToList();
        return dtos;
    }

    public async Task<MatrizProveedorResumenDto> MatrizResumenAsync(ReporteProveedorFiltro f, CancellationToken ct = default)
    {
        var lista = await MatrizQueryAsync(f, ct);
        var dtos = lista.Select(MapMatriz).ToList();

        var r = new MatrizProveedorResumenDto
        {
            TotalReportes = dtos.Count,
            Abiertos = dtos.Count(d => d.Estado != (int)EstadoReporteProveedor.Cerrado && d.Estado != (int)EstadoReporteProveedor.Resuelto),
            Cerrados = dtos.Count(d => d.Estado == (int)EstadoReporteProveedor.Cerrado || d.Estado == (int)EstadoReporteProveedor.Resuelto),
            ConAsistencia = dtos.Count(d => d.BrindoAsistencia == true),
            SinAsistencia = dtos.Count(d => d.BrindoAsistencia == false),
            // Cumplimiento = lo que marcó el usuario
            SlaCumplidos = dtos.Count(d => d.CumplioSla == true),
            SlaIncumplidos = dtos.Count(d => d.CumplioSla == false),
            SlaSinEvaluar = dtos.Count(d => d.CumplioSla == null)
        };
        var evaluados = r.SlaCumplidos + r.SlaIncumplidos;
        r.PctCumplimientoSla = evaluados > 0 ? Math.Round(100.0 * r.SlaCumplidos / evaluados, 1) : 0;

        r.PorProveedor = dtos.GroupBy(d => d.Proveedor).Select(g => new ConteoProveedor { Etiqueta = g.Key, Cantidad = g.Count() }).OrderByDescending(x => x.Cantidad).ToList();
        r.PorResponsable = dtos.Where(d => !string.IsNullOrEmpty(d.ResponsableInterno)).GroupBy(d => d.ResponsableInterno!).Select(g => new ConteoProveedor { Etiqueta = g.Key, Cantidad = g.Count() }).OrderByDescending(x => x.Cantidad).ToList();
        r.PorSeveridad = dtos.GroupBy(d => d.Urgencia).Select(g => new ConteoProveedor { Etiqueta = g.Key, Cantidad = g.Count() }).ToList();
        r.PorEstado = dtos.GroupBy(d => d.EstadoTexto).Select(g => new ConteoProveedor { Etiqueta = g.Key, Cantidad = g.Count() }).ToList();

        // Matriz de control por severidad (como la hoja "Matriz Control")
        foreach (var sev in SlaProveedorCatalogo.Severidades)
        {
            var delNivel = dtos.Where(d => string.Equals(d.Urgencia, sev, StringComparison.OrdinalIgnoreCase)).ToList();
            var cant = delNivel.Count;
            var dentro = delNivel.Count(d => d.CumplioSla == true);
            var evaluadosNivel = delNivel.Count(d => d.CumplioSla != null);
            // SLA de referencia: si hay un proveedor filtrado, usa el suyo; si no, deja el texto genérico
            int? slaRef = delNivel.FirstOrDefault(d => d.SlaReferenciaHoras.HasValue)?.SlaReferenciaHoras;
            r.Control.Add(new FilaMatrizControl
            {
                Severidad = sev,
                SlaTexto = SlaHorasTexto(slaRef),
                CantidadSoportes = cant,
                ResueltosDentroSla = dentro,
                PctCumplimiento = evaluadosNivel > 0 ? Math.Round(100.0 * dentro / evaluadosNivel, 0) : 0
            });
        }
        return r;
    }

    public async Task<ReporteProveedorMatrizDto> MatrizGuardarAsync(GuardarReporteProveedorRequest req, CancellationToken ct = default)
    {
        ReporteProveedor r;
        if (req.ReporteId.HasValue)
        {
            r = await _db.ReportesProveedor.FirstOrDefaultAsync(x => x.ReporteId == req.ReporteId.Value, ct)
                ?? throw new InvalidOperationException("Reporte no encontrado.");
            r.ModificadoEn = DateTime.UtcNow;
            r.ModificadoPor = req.Usuario;
        }
        else
        {
            r = new ReporteProveedor { CreadoEn = DateTime.UtcNow, CreadoPor = req.Usuario };
            _db.ReportesProveedor.Add(r);
        }

        r.ProveedorId = req.ProveedorId;
        r.RefNumSistema = req.RefNumSistema;
        r.TipoVinculo = req.TipoVinculo;
        r.NumeroCasoExterno = req.NumeroCasoExterno;
        r.Titulo = req.Titulo;
        r.Descripcion = req.Descripcion;
        r.FechaReporte = req.FechaReporte;
        r.ContextoEnviado = req.ContextoEnviado;
        r.RespuestasRecibidas = req.RespuestasRecibidas;
        r.Urgencia = string.IsNullOrWhiteSpace(req.Urgencia) ? "Medio" : req.Urgencia;

        // SLA de referencia = el del proveedor para esa severidad
        var prov = await _db.Proveedores.AsNoTracking().FirstOrDefaultAsync(p => p.ProveedorId == r.ProveedorId, ct);
        r.SlaHorasRespuesta = req.SlaHorasRespuesta ?? r.Urgencia.ToLowerInvariant() switch
        {
            "critico" => prov?.SlaCriticoHoras,
            "alto" => prov?.SlaAltoHoras,
            "medio" => prov?.SlaMedioHoras,
            "bajo" => prov?.SlaBajoHoras,
            _ => prov?.SlaMedioHoras
        };
        r.FechaPrimeraRespuesta = req.FechaPrimeraRespuesta;
        r.BrindoAsistencia = req.BrindoAsistencia;
        r.EntregoCambio = req.EntregoCambio;
        r.CumplioSla = req.CumplioSla;
        r.Estado = (EstadoReporteProveedor)req.Estado;
        r.ResponsableInterno = req.ResponsableInterno;
        r.ResultadoSolucion = req.ResultadoSolucion;

        await _db.SaveChangesAsync(ct);
        r.Proveedor = prov;
        return MapMatriz(r);
    }

    public async Task MatrizEliminarAsync(long reporteId, CancellationToken ct = default)
    {
        var r = await _db.ReportesProveedor.FirstOrDefaultAsync(x => x.ReporteId == reporteId, ct);
        if (r != null) { _db.ReportesProveedor.Remove(r); await _db.SaveChangesAsync(ct); }
    }

    public async Task<IReadOnlyList<string>> MatrizResponsablesAsync(CancellationToken ct = default)
        => await _db.ReportesProveedor.AsNoTracking().Where(r => r.ResponsableInterno != null && r.ResponsableInterno != "")
            .Select(r => r.ResponsableInterno!).Distinct().OrderBy(x => x).ToListAsync(ct);

    public async Task<IReadOnlyList<ReporteProveedorMatrizDto>> MatrizPorRefNumSistemaAsync(string refNum, CancellationToken ct = default)
    {
        var lista = await _db.ReportesProveedor.AsNoTracking().Include(r => r.Proveedor)
            .Where(r => r.RefNumSistema == refNum)
            .OrderByDescending(r => r.FechaReporte).ToListAsync(ct);
        return lista.Select(MapMatriz).ToList();
    }

    // ===== CRUD de catálogo de proveedores =====

    public async Task<IReadOnlyList<ProveedorCatalogoDto>> CatalogoListarAsync(CancellationToken ct = default)
        => await _db.Proveedores.AsNoTracking().OrderBy(p => p.Nombre).Select(p => new ProveedorCatalogoDto
        {
            ProveedorId = p.ProveedorId, Nombre = p.Nombre, Contacto = p.Contacto, Email = p.Email,
            Telefono = p.Telefono, Activo = p.Activo,
            SlaCriticoHoras = p.SlaCriticoHoras, SlaAltoHoras = p.SlaAltoHoras,
            SlaMedioHoras = p.SlaMedioHoras, SlaBajoHoras = p.SlaBajoHoras
        }).ToListAsync(ct);

    public async Task<ProveedorCatalogoDto> CatalogoGuardarAsync(GuardarProveedorRequest req, CancellationToken ct = default)
    {
        Proveedor p;
        if (req.ProveedorId.HasValue)
        {
            p = await _db.Proveedores.FirstOrDefaultAsync(x => x.ProveedorId == req.ProveedorId.Value, ct)
                ?? throw new InvalidOperationException("Proveedor no encontrado.");
        }
        else { p = new Proveedor(); _db.Proveedores.Add(p); }

        p.Nombre = req.Nombre;
        p.Contacto = req.Contacto;
        p.Email = req.Email;
        p.Telefono = req.Telefono;
        p.Activo = req.Activo;
        p.SlaCriticoHoras = req.SlaCriticoHoras;
        p.SlaAltoHoras = req.SlaAltoHoras;
        p.SlaMedioHoras = req.SlaMedioHoras;
        p.SlaBajoHoras = req.SlaBajoHoras;

        await _db.SaveChangesAsync(ct);
        return new ProveedorCatalogoDto
        {
            ProveedorId = p.ProveedorId, Nombre = p.Nombre, Contacto = p.Contacto, Email = p.Email,
            Telefono = p.Telefono, Activo = p.Activo,
            SlaCriticoHoras = p.SlaCriticoHoras, SlaAltoHoras = p.SlaAltoHoras,
            SlaMedioHoras = p.SlaMedioHoras, SlaBajoHoras = p.SlaBajoHoras
        };
    }

    public async Task CatalogoEliminarAsync(int proveedorId, CancellationToken ct = default)
    {
        var tieneReportes = await _db.ReportesProveedor.AnyAsync(r => r.ProveedorId == proveedorId, ct);
        var p = await _db.Proveedores.FirstOrDefaultAsync(x => x.ProveedorId == proveedorId, ct);
        if (p == null) return;
        if (tieneReportes) { p.Activo = false; await _db.SaveChangesAsync(ct); }  // no borrar si tiene historial
        else { _db.Proveedores.Remove(p); await _db.SaveChangesAsync(ct); }
    }
}
