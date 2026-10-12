using TicketAnalytics.Domain.Enums;

namespace TicketAnalytics.Application.Common.Interfaces;

public interface IComplejidadService
{
    Task AsignarAsync(string chgRefNum, NivelComplejidad nivel, string? asignadoPor, CancellationToken ct = default);
    Task<NivelComplejidad> ObtenerAsync(string chgRefNum, CancellationToken ct = default);
}
