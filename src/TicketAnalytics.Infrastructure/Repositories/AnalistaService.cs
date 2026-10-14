using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Analistas.DTOs;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Tickets.DTOs;
using TicketAnalytics.Domain.Common;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;
using TicketAnalytics.Infrastructure.Services.Parametros;

namespace TicketAnalytics.Infrastructure.Repositories;

/// <summary>
/// Métricas de productividad por analista del grupo, según el rango de fechas.
///
/// Antes: se traían a memoria TODAS las filas de recibidos, cerrados y OC del rango (de cualquier
/// asignatario) y se agrupaba en C# comparando binarios con SequenceEqual.
/// Ahora: cada bloque es un GROUP BY (analista, día) en SQL, restringido a los UUID del grupo
/// (call_req_x4 / chg_x4). A memoria llegan, como mucho, analistas × días con actividad.
/// </summary>
public class AnalistaService : IAnalistaService
{
    private readonly SdmReadContext _sdm;
    private readonly AnalyticsContext _analytics;
    private readonly IParametrosProvider _parametros;
    private readonly CatalogoSdm _catalogo;

    public AnalistaService(SdmReadContext sdm, AnalyticsContext analytics, IParametrosProvider parametros, CatalogoSdm catalogo)
    {
        _sdm = sdm;
        _analytics = analytics;
        _parametros = parametros;
        _catalogo = catalogo;
    }

    /// <summary>Medianoche de referencia para numerar días en SQL sin funciones sobre la columna en el WHERE.</summary>
    private static readonly DateTime Dia0 = new(2000, 1, 1);
    private static readonly double OffsetDia0 = (Dia0 - EpochRD.BaseTicket).TotalSeconds;

    public async Task<IReadOnlyList<MetricaAnalistaDto>> MetricasAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var p = await _parametros.ObtenerAsync(ct);
        var alcance = (await _catalogo.AlcanceAsync(ct)).Select(a => a.Persid).ToList();
        if (f.Categorias is { Count: > 0 }) alcance = alcance.Where(x => f.Categorias.Contains(x)).ToList();
        int desde = f.FechaDesde.HasValue ? EpochRD.InicioDelDia(f.FechaDesde.Value) : EpochRD.InicioDelDia(p.FechaBase);
        int hasta = f.FechaHasta.HasValue ? EpochRD.FinDelDiaExclusivo(f.FechaHasta.Value) : EpochRD.ToEpoch(DateTime.Now);

        var grupo = await _catalogo.GrupoAsync(ct);
        if (grupo.Count == 0) return new List<MetricaAnalistaDto>();
        var uuids = grupo.Select(g => g.Uuid).ToList();
        var codigosOc = (await _catalogo.CategoriasOcAreaAsync(ct)).Keys.ToList();

        // --- Tickets recibidos: GROUP BY (assignee, día de apertura) ---
        var recibidos = await ConsultasSdm.FiltrarPorUuid(
                _sdm.CallReqs.AsNoTracking().Where(cr => alcance.Contains(cr.category!) && cr.open_date >= desde && cr.open_date < hasta),
                cr => cr.assignee, uuids)
            .GroupBy(cr => new { cr.assignee, Dia = Math.Floor((cr.open_date!.Value - OffsetDia0) / 86400.0) })
            .Select(g => new { g.Key.assignee, g.Key.Dia, N = g.Count(), Activos = g.Count(x => x.active_flag == 1) })
            .ToListAsync(ct);

        // --- Tickets cerrados: GROUP BY (assignee, día de cierre), con la suma de segundos de resolución ---
        var cerrados = await ConsultasSdm.FiltrarPorUuid(
                _sdm.CallReqs.AsNoTracking().Where(cr => alcance.Contains(cr.category!) && cr.close_date >= desde && cr.close_date < hasta && cr.close_date > 0),
                cr => cr.assignee, uuids)
            .GroupBy(cr => new { cr.assignee, Dia = Math.Floor((cr.close_date!.Value - OffsetDia0) / 86400.0) })
            .Select(g => new
            {
                g.Key.assignee, g.Key.Dia, N = g.Count(),
                ConApertura = g.Count(x => x.open_date != null && x.open_date != 0),
                Segundos = g.Sum(x => x.open_date != null && x.open_date != 0 ? (long)x.close_date!.Value - x.open_date.Value : 0L)
            })
            .ToListAsync(ct);

        // --- OC recibidas / cerradas: GROUP BY (assignee, día) sobre las categorías de OC del área ---
        var ocRecibidas = await ConsultasSdm.FiltrarPorUuid(
                _sdm.Changes.AsNoTracking().Where(c => c.category != null && codigosOc.Contains(c.category) && c.open_date >= desde && c.open_date < hasta),
                c => c.assignee, uuids)
            .GroupBy(c => new { c.assignee, Dia = Math.Floor((c.open_date!.Value - OffsetDia0) / 86400.0) })
            .Select(g => new { g.Key.assignee, g.Key.Dia, N = g.Count() })
            .ToListAsync(ct);
        var ocCerradas = await ConsultasSdm.FiltrarPorUuid(
                _sdm.Changes.AsNoTracking().Where(c => c.category != null && codigosOc.Contains(c.category) && c.close_date >= desde && c.close_date < hasta && c.close_date > 0),
                c => c.assignee, uuids)
            .GroupBy(c => new { c.assignee, Dia = Math.Floor((c.close_date!.Value - OffsetDia0) / 86400.0) })
            .Select(g => new { g.Key.assignee, g.Key.Dia, N = g.Count() })
            .ToListAsync(ct);

        // Seguimiento local por técnico (base local). TecnicoUuid no lo llena ningún flujo actual;
        // la gestión guarda el nombre en TecnicoResponsable: se usa el UUID si existe y si no, el nombre.
        var seguimientos = await _analytics.TicketSeguimientos.AsNoTracking()
            .Select(s => new { s.TecnicoUuid, s.TecnicoResponsable, s.EtapaActual, s.ProximoSeguimiento }).ToListAsync(ct);
        var actualizaciones = await _analytics.SeguimientoActualizaciones.AsNoTracking()
            .Where(a => a.CreadoEn >= (f.FechaDesde ?? DateTime.MinValue))
            .GroupBy(a => a.Autor).Select(g => new { Autor = g.Key, N = g.Count() }).ToListAsync(ct);

        bool Es(byte[]? a, byte[] b) => a != null && a.SequenceEqual(b);

        var result = new List<MetricaAnalistaDto>();
        foreach (var m in grupo)
        {
            var rec = recibidos.Where(x => Es(x.assignee, m.Uuid)).ToList();
            var cer = cerrados.Where(x => Es(x.assignee, m.Uuid)).ToList();
            var ocRec = ocRecibidas.Where(x => Es(x.assignee, m.Uuid)).ToList();
            var ocCer = ocCerradas.Where(x => Es(x.assignee, m.Uuid)).ToList();
            // Como antes: solo aparecen analistas con algún movimiento en el período.
            if (rec.Count == 0 && cer.Count == 0 && ocRec.Count == 0 && ocCer.Count == 0) continue;

            int totRec = rec.Sum(x => x.N), totCer = cer.Sum(x => x.N), totOcRec = ocRec.Sum(x => x.N), totOcCer = ocCer.Sum(x => x.N);
            int diasRec = rec.Count, diasCer = cer.Count, diasOcRec = ocRec.Count, diasOcCer = ocCer.Count;
            var conApertura = cer.Sum(x => x.ConApertura);
            var segundos = cer.Sum(x => x.Segundos);

            var segs = seguimientos.Where(s => Es(s.TecnicoUuid, m.Uuid)
                                              || (s.TecnicoUuid == null && string.Equals(s.TecnicoResponsable, m.Nombre, StringComparison.OrdinalIgnoreCase))).ToList();

            result.Add(new MetricaAnalistaDto
            {
                Analista = m.Nombre,
                AnalistaUuid = m.UuidHex,
                TotalRecibidos = totRec,
                TotalCerrados = totCer,
                TotalActivos = rec.Sum(x => x.Activos),
                DiasConApertura = diasRec,
                DiasConCierre = diasCer,
                PromedioRecibidosDia = diasRec > 0 ? Math.Round((decimal)totRec / diasRec, 2) : 0,
                PromedioCerradosDia = diasCer > 0 ? Math.Round((decimal)totCer / diasCer, 2) : 0,
                PromedioHorasResolucion = conApertura > 0 ? Math.Round(segundos / 3600.0 / conApertura, 1) : 0,
                OcRecibidas = totOcRec,
                OcCerradas = totOcCer,
                OcDiasApertura = diasOcRec,
                OcDiasCierre = diasOcCer,
                OcPromedioRecibidasDia = diasOcRec > 0 ? Math.Round((decimal)totOcRec / diasOcRec, 2) : 0,
                OcPromedioCerradasDia = diasOcCer > 0 ? Math.Round((decimal)totOcCer / diasOcCer, 2) : 0,
                CasosPendientesSeguimiento = segs.Count(s => s.ProximoSeguimiento != null && s.ProximoSeguimiento <= DateTime.Now),
                CasosEnEsperaProveedor = segs.Count(s => (int)s.EtapaActual == 3), // EnEsperaProveedor
                ActualizacionesRealizadas = actualizaciones.Where(a => a.Autor == m.Nombre).Sum(a => a.N)
            });
        }
        return result.OrderByDescending(x => x.TotalRecibidos).ToList();
    }

    public async Task<MetricaAnalistaDto?> MetricaDeAnalistaAsync(string uuidHex, TicketFiltro f, CancellationToken ct = default)
    {
        var todas = await MetricasAsync(f, ct);
        var hex = uuidHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? uuidHex : "0x" + uuidHex;
        return todas.FirstOrDefault(m => string.Equals(m.AnalistaUuid, hex, StringComparison.OrdinalIgnoreCase));
    }
}
