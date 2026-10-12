using TicketAnalytics.Application.Casos.DTOs;

namespace TicketAnalytics.Application.Common.Interfaces;

/// <summary>Registra toda la gestión de un caso en una única operación transaccional.</summary>
public interface IGestionCasoService
{
    Task<GestionCasoResultado> RegistrarAsync(GestionCasoRequest req, CancellationToken ct = default);
}
