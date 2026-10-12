using TicketAnalytics.Application.Proveedores.DTOs;

namespace TicketAnalytics.Application.Common.Interfaces;

public interface IJiraService
{
    Task<JiraDto> VincularAsync(string refNum, string claveJira, string? titulo, string? url, string? autor, CancellationToken ct = default);
    Task<IReadOnlyList<JiraDto>> DeSeguimientoAsync(string refNum, CancellationToken ct = default);
    Task DesvincularAsync(long jiraId, CancellationToken ct = default);
}
