using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Domain.Entities;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;

namespace TicketAnalytics.Infrastructure.Services;

/// <summary>Captura snapshots de tickets en alcance (lee MDB) y registra deltas en la base LOCAL.</summary>
public class SnapshotService : ISnapshotService
{
    private readonly SdmReadContext _sdm;
    private readonly AnalyticsContext _analytics;
    public SnapshotService(SdmReadContext sdm, AnalyticsContext analytics) { _sdm = sdm; _analytics = analytics; }

    public async Task<int> CapturarYDetectarCambiosAsync(CancellationToken ct = default)
    {
        var alcance = await _analytics.CategoriasAlcance.AsNoTracking().Where(c => c.Activo).Select(c => c.Persid).ToListAsync(ct);
        var actuales = await _sdm.CallReqs.AsNoTracking().Where(cr => alcance.Contains(cr.category!))
            .Select(cr => new { cr.id, cr.ref_num, cr.persid, cr.category, cr.status, cr.assignee, cr.group_id, cr.open_date, cr.close_date, cr.sla_violation, cr.change }).ToListAsync(ct);

        var crIds = actuales.Select(a => a.id).ToList();
        var ultimos = await _analytics.TicketSnapshots.Where(s => crIds.Contains(s.CrId))
            .GroupBy(s => s.CrId).Select(g => g.OrderByDescending(x => x.CapturadoEn).First()).ToListAsync(ct);
        var ultimoPorCr = ultimos.ToDictionary(s => s.CrId);

        int cambios = 0;
        var nuevosSnap = new List<TicketSnapshot>();
        var nuevosLog = new List<TicketChangeLog>();

        foreach (var a in actuales)
        {
            var hash = Hash($"{a.status}|{ToHex(a.assignee)}|{ToHex(a.group_id)}|{a.close_date}|{a.sla_violation}|{a.change}");
            ultimoPorCr.TryGetValue(a.id, out var prev);
            if (prev?.HashEstado != null && prev.HashEstado.SequenceEqual(hash)) continue;

            if (prev != null)
            {
                cambios += Delta(nuevosLog, a.id, a.ref_num, "Estado", prev.Estado, a.status);
                cambios += Delta(nuevosLog, a.id, a.ref_num, "Assignee", ToHex(prev.AssigneeUuid), ToHex(a.assignee));
                cambios += Delta(nuevosLog, a.id, a.ref_num, "Grupo", ToHex(prev.GroupUuid), ToHex(a.group_id));
                cambios += Delta(nuevosLog, a.id, a.ref_num, "CloseDate", prev.CloseDateEpoch?.ToString(), a.close_date?.ToString());
                cambios += Delta(nuevosLog, a.id, a.ref_num, "SlaViolation", prev.SlaViolation?.ToString(), a.sla_violation?.ToString());
                cambios += Delta(nuevosLog, a.id, a.ref_num, "OrdenCambio", prev.ChangeId?.ToString(), a.change?.ToString());
            }

            nuevosSnap.Add(new TicketSnapshot
            {
                CrId = a.id, RefNum = a.ref_num, Persid = a.persid, Categoria = a.category ?? "", Estado = a.status,
                AssigneeUuid = a.assignee, GroupUuid = a.group_id, OpenDateEpoch = a.open_date, CloseDateEpoch = a.close_date,
                SlaViolation = a.sla_violation, ChangeId = a.change, HashEstado = hash, CapturadoEn = DateTime.UtcNow
            });
        }

        if (nuevosSnap.Count > 0) _analytics.TicketSnapshots.AddRange(nuevosSnap);
        if (nuevosLog.Count > 0) _analytics.TicketChangeLogs.AddRange(nuevosLog);
        await _analytics.SaveChangesAsync(ct);
        return cambios;
    }

    private static int Delta(List<TicketChangeLog> logs, int crId, string refNum, string campo, string? ant, string? nue)
    {
        if (string.Equals(ant, nue, StringComparison.Ordinal)) return 0;
        logs.Add(new TicketChangeLog { CrId = crId, RefNum = refNum, Campo = campo, ValorAnterior = ant, ValorNuevo = nue });
        return 1;
    }
    private static byte[] Hash(string s) => SHA256.HashData(Encoding.UTF8.GetBytes(s));
    private static string? ToHex(byte[]? b) => b == null ? null : "0x" + Convert.ToHexString(b);
}
