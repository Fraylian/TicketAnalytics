using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Sla.DTOs;
using TicketAnalytics.Application.Tickets.DTOs;
using TicketAnalytics.Domain.Common;
using TicketAnalytics.Infrastructure.Persistence.Sdm;
using TicketAnalytics.Infrastructure.Services.Parametros;

namespace TicketAnalytics.Infrastructure.Repositories;

/// <summary>Fila base de SLA: un ticket del alcance con su PRIMER attached_sla.</summary>
public sealed class FilaSla
{
    public int Id { get; set; }
    public string RefNum { get; set; } = "";
    public int? OpenDate { get; set; }
    public int? CloseDate { get; set; }
    public int? Ttv { get; set; }
    public int? Viol { get; set; }
}

/// <summary>
/// SLA con tiempos reales (call_req + attached_sla de MDB).
///
/// Antes: CalcularTodosAsync traía TODOS los tickets del rango y TODOS sus SLA a memoria y
/// calculaba cada uno en C#, para luego contar o filtrar. Cada pantalla repetía esa carga.
/// Ahora: una consulta SQL explícita (parametrizada) une cada ticket con su primer SLA
/// (OUTER APPLY TOP 1, índice attached_sla_x0) y sobre ella se hacen los conteos con GROUP BY
/// y los listados con ORDER BY + TOP. Las reglas son las mismas de CalculoSla.
/// </summary>
public class SlaService : ISlaService
{
    private const int MaxListado = 200;
    private readonly SdmReadContext _sdm;
    private readonly IParametrosProvider _parametros;
    private readonly CatalogoSdm _catalogo;

    public SlaService(SdmReadContext sdm, IParametrosProvider parametros, CatalogoSdm catalogo)
    {
        _sdm = sdm;
        _parametros = parametros;
        _catalogo = catalogo;
    }

    /// <summary>
    /// Tickets del alcance en el rango de apertura, cada uno con su primer SLA.
    /// Índices esperados: call_req_x7 (category, active_flag, type) / call_req_x0 (open_date);
    /// attached_sla_x0 (ticket_id, ticket_type) para el OUTER APPLY.
    /// Las categorías viajan como UN parámetro JSON (OPENJSON): sin límite de parámetros.
    /// </summary>
    private async Task<IQueryable<FilaSla>> BaseAsync(TicketFiltro f, CancellationToken ct)
    {
        var p = await _parametros.ObtenerAsync(ct);
        var alcance = (await _catalogo.AlcanceAsync(ct)).Select(a => a.Persid).ToList();
        if (f.Categorias is { Count: > 0 }) alcance = alcance.Where(c => f.Categorias.Contains(c)).ToList();
        var (desde, hasta) = EpochRD.Rango(f.FechaDesde, f.FechaHasta, p.FechaBase);
        var hastaE = hasta ?? int.MaxValue;
        var alcanceJson = JsonSerializer.Serialize(alcance);

        return _sdm.Database.SqlQuery<FilaSla>($@"
SELECT cr.id AS Id, cr.ref_num AS RefNum, cr.open_date AS OpenDate, cr.close_date AS CloseDate,
       s.time_to_violation AS Ttv, s.sla_viol_status AS Viol
FROM dbo.call_req AS cr
OUTER APPLY (SELECT TOP (1) a.time_to_violation, a.sla_viol_status
             FROM dbo.attached_sla AS a
             WHERE a.ticket_id = cr.id AND a.ticket_type = N'cr' AND a.del = 0
             ORDER BY a.id) AS s
WHERE cr.category IN (SELECT j.value FROM OPENJSON({alcanceJson}) WITH (value NVARCHAR(60) '$') AS j)
  AND cr.open_date >= {desde} AND cr.open_date < {hastaE}");
    }

    public async Task<ResumenSlaDto> ResumenAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var p = await _parametros.ObtenerAsync(ct);
        var q = await BaseAsync(f, ct);
        int ahora = ConsultaOrdenesCambio.Reloj.Limite(DateTime.Now);
        double pct = p.TicketPctRiesgoSla;

        // Un solo GROUP BY con todos los conteos (reglas idénticas a CalculoSla).
        var k = await q.GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(),
            ConSla = g.Count(x => x.Ttv != null && x.Ttv != 0),
            Dentro = g.Count(x => x.Ttv != null && x.Ttv != 0 && x.CloseDate > 0 && x.CloseDate <= x.Ttv),
            Vencidos = g.Count(x => x.Ttv != null && x.Ttv != 0
                                    && ((x.CloseDate > 0 && x.CloseDate > x.Ttv)
                                        || ((x.CloseDate == null || x.CloseDate <= 0) && (x.Viol == 1 || x.Ttv < ahora)))),
            EnCurso = g.Count(x => x.Ttv != null && x.Ttv != 0 && (x.CloseDate == null || x.CloseDate <= 0)
                                   && (x.Viol == null || x.Viol != 1) && x.Ttv >= ahora),
            Proximos = g.Count(x => x.Ttv != null && x.Ttv != 0 && (x.CloseDate == null || x.CloseDate <= 0)
                                    && (x.Viol == null || x.Viol != 1) && x.Ttv >= ahora
                                    && x.OpenDate != null && x.OpenDate != 0 && x.Ttv > x.OpenDate
                                    && 100.0 * (ahora - x.OpenDate.Value) >= pct * (x.Ttv.Value - x.OpenDate.Value)),
            Cerrados = g.Count(x => x.CloseDate != null && x.CloseDate != 0 && x.OpenDate != null && x.OpenDate != 0),
            SegundosResolucion = g.Sum(x => x.CloseDate != null && x.CloseDate != 0 && x.OpenDate != null && x.OpenDate != 0
                                            ? (long)x.CloseDate.Value - x.OpenDate.Value : 0L)
        }).FirstOrDefaultAsync(ct);

        if (k is null) return new ResumenSlaDto();
        return new ResumenSlaDto
        {
            Total = k.Total,
            DentroSla = k.Dentro,
            Vencidos = k.Vencidos,
            ProximosAVencer = k.Proximos,
            EnCurso = k.EnCurso,
            PctCumplimiento = k.ConSla > 0 ? Math.Round(100m * k.Dentro / k.ConSla, 2) : 0,
            PromedioHorasResolucion = k.Cerrados > 0 ? Math.Round(k.SegundosResolucion / 3600.0 / k.Cerrados, 1) : 0
        };
    }

    /// <summary>Abiertos en curso con el SLA consumido sobre el umbral, los que vencen primero arriba (máx. 200).</summary>
    public async Task<IReadOnlyList<SlaTicketDto>> TicketsProximosAVencerAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var p = await _parametros.ObtenerAsync(ct);
        var q = await BaseAsync(f, ct);
        int ahora = ConsultaOrdenesCambio.Reloj.Limite(DateTime.Now);
        double pct = p.TicketPctRiesgoSla;

        var filas = await q.Where(x => x.Ttv != null && x.Ttv != 0 && (x.CloseDate == null || x.CloseDate <= 0)
                                       && (x.Viol == null || x.Viol != 1) && x.Ttv >= ahora
                                       && x.OpenDate != null && x.OpenDate != 0 && x.Ttv > x.OpenDate
                                       && 100.0 * (ahora - x.OpenDate.Value) >= pct * (x.Ttv.Value - x.OpenDate.Value))
            .OrderBy(x => x.Ttv).ThenBy(x => x.Id).Take(MaxListado).ToListAsync(ct);
        return filas.Select(x => Dto(x, p.TicketPctRiesgoSla)).Where(t => t.ProximoAVencer).ToList();
    }

    /// <summary>Vencidos (cerrados tarde o abiertos ya vencidos), los más consumidos arriba (máx. 200).</summary>
    public async Task<IReadOnlyList<SlaTicketDto>> TicketsVencidosAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var p = await _parametros.ObtenerAsync(ct);
        var q = await BaseAsync(f, ct);
        int ahora = ConsultaOrdenesCambio.Reloj.Limite(DateTime.Now);

        var filas = await q.Where(x => x.Ttv != null && x.Ttv != 0
                                       && ((x.CloseDate > 0 && x.CloseDate > x.Ttv)
                                           || ((x.CloseDate == null || x.CloseDate <= 0) && (x.Viol == 1 || x.Ttv < ahora))))
            // % consumido = (cierre o ahora - apertura) / (vencimiento - apertura); sin ventana válida van al final.
            .OrderByDescending(x => x.OpenDate != null && x.OpenDate != 0 && x.Ttv > x.OpenDate
                ? (double)((x.CloseDate > 0 ? x.CloseDate!.Value : ahora) - x.OpenDate.Value) / (x.Ttv!.Value - x.OpenDate.Value)
                : (double?)null)
            .ThenBy(x => x.Id)
            .Take(MaxListado).ToListAsync(ct);
        return filas.Select(x => Dto(x, p.TicketPctRiesgoSla)).ToList();
    }

    public async Task<SlaTicketDto?> SlaDeTicketAsync(string refNum, CancellationToken ct = default)
    {
        // call_req_x3 (ref_num) + attached_sla_x0.
        var cr = await _sdm.CallReqs.AsNoTracking().Where(x => x.ref_num == refNum)
            .Select(x => new { x.id, x.ref_num, x.open_date, x.close_date }).FirstOrDefaultAsync(ct);
        if (cr is null) return null;
        var sla = await _sdm.AttachedSlas.AsNoTracking()
            .Where(s => s.ticket_type == "cr" && s.del == 0 && s.ticket_id == cr.id).OrderBy(s => s.id)
            .Select(s => new { s.time_to_violation, s.sla_viol_status }).FirstOrDefaultAsync(ct);
        var p = await _parametros.ObtenerAsync(ct);
        return Dto(new FilaSla { Id = cr.id, RefNum = cr.ref_num, OpenDate = cr.open_date, CloseDate = cr.close_date,
                                 Ttv = sla?.time_to_violation, Viol = sla?.sla_viol_status }, p.TicketPctRiesgoSla);
    }

    private static SlaTicketDto Dto(FilaSla x, int pctRiesgo)
    {
        var r = CalculoSla.Calcular(x.OpenDate, x.CloseDate, x.Ttv, x.Viol, DateTime.Now, pctRiesgo);
        return new SlaTicketDto
        {
            NoTicket = x.RefNum, EstadoSla = r.EstadoTexto,
            FechaApertura = r.FechaApertura, FechaVencimiento = r.FechaVencimiento, FechaCierre = r.FechaCierre,
            HorasTranscurridas = r.HorasTranscurridas, HorasRestantes = r.HorasRestantes,
            PctConsumido = r.PctConsumido, ProximoAVencer = r.ProximoAVencer, Vencido = r.Vencido
        };
    }
}
