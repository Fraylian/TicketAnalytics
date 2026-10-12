using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Domain.Entities;
using TicketAnalytics.Domain.Enums;
using TicketAnalytics.Infrastructure.Persistence.Analytics;

namespace TicketAnalytics.Infrastructure.Services;

public class ComplejidadService : IComplejidadService
{
    private readonly AnalyticsContext _analytics;
    public ComplejidadService(AnalyticsContext analytics) => _analytics = analytics;

    public async Task AsignarAsync(string chgRefNum, NivelComplejidad nivel, string? asignadoPor, CancellationToken ct = default)
    {
        var ex = await _analytics.OrdenCambioComplejidades.FirstOrDefaultAsync(o => o.ChgRefNum == chgRefNum, ct);
        if (ex == null)
            _analytics.OrdenCambioComplejidades.Add(new OrdenCambioComplejidad { ChgRefNum = chgRefNum, Complejidad = nivel, AsignadoPor = asignadoPor, AsignadoEn = DateTime.UtcNow });
        else { ex.Complejidad = nivel; ex.AsignadoPor = asignadoPor; ex.AsignadoEn = DateTime.UtcNow; }
        await _analytics.SaveChangesAsync(ct);
    }

    public async Task<NivelComplejidad> ObtenerAsync(string chgRefNum, CancellationToken ct = default)
    {
        var o = await _analytics.OrdenCambioComplejidades.AsNoTracking().FirstOrDefaultAsync(x => x.ChgRefNum == chgRefNum, ct);
        return o?.Complejidad ?? NivelComplejidad.SinAsignar;
    }
}
