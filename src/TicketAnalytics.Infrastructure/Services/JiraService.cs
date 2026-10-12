using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Proveedores.DTOs;
using TicketAnalytics.Domain.Entities;
using TicketAnalytics.Infrastructure.Persistence.Analytics;

namespace TicketAnalytics.Infrastructure.Services;

public class JiraService : IJiraService
{
    private readonly AnalyticsContext _db;
    public JiraService(AnalyticsContext db) => _db = db;

    public async Task<JiraDto> VincularAsync(string refNum, string claveJira, string? titulo, string? url, string? autor, CancellationToken ct = default)
    {
        var seg = await _db.TicketSeguimientos.FirstOrDefaultAsync(s => s.RefNum == refNum, ct)
                  ?? throw new InvalidOperationException($"No existe seguimiento para {refNum}.");
        var j = new JiraRelacion { SeguimientoId = seg.SeguimientoId, ClaveJira = claveJira, Titulo = titulo, Url = url, VinculadoPor = autor };
        _db.JiraRelaciones.Add(j);
        await _db.SaveChangesAsync(ct);
        return new JiraDto { JiraId = j.JiraId, SeguimientoId = j.SeguimientoId, ClaveJira = j.ClaveJira, Titulo = j.Titulo, Estado = (int)j.Estado, Url = j.Url };
    }

    public async Task<IReadOnlyList<JiraDto>> DeSeguimientoAsync(string refNum, CancellationToken ct = default)
    {
        var seg = await _db.TicketSeguimientos.AsNoTracking().FirstOrDefaultAsync(s => s.RefNum == refNum, ct);
        if (seg is null) return Array.Empty<JiraDto>();
        return await _db.JiraRelaciones.AsNoTracking().Where(j => j.SeguimientoId == seg.SeguimientoId)
            .Select(j => new JiraDto { JiraId = j.JiraId, SeguimientoId = j.SeguimientoId, ClaveJira = j.ClaveJira, Titulo = j.Titulo, Estado = (int)j.Estado, Url = j.Url })
            .ToListAsync(ct);
    }

    public async Task DesvincularAsync(long jiraId, CancellationToken ct = default)
    {
        var j = await _db.JiraRelaciones.FindAsync(new object[] { jiraId }, ct);
        if (j != null) { _db.JiraRelaciones.Remove(j); await _db.SaveChangesAsync(ct); }
    }
}
