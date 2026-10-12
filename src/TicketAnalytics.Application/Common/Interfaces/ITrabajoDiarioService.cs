using TicketAnalytics.Application.Common.Models;
using TicketAnalytics.Application.TrabajoDiario.DTOs;

namespace TicketAnalytics.Application.Common.Interfaces;

public interface ITrabajoDiarioService
{
    Task<IReadOnlyList<TecnicoDto>> ListarTecnicosAsync(CancellationToken ct = default);
    Task<int> SincronizarTecnicosDesdeMdbAsync(CancellationToken ct = default);
    Task<TecnicoDto> CrearTecnicoAsync(string nombre, CancellationToken ct = default);

    Task<PagedResult<ActividadDto>> BuscarAsync(ActividadFiltro f, CancellationToken ct = default);
    /// <summary>Todas las actividades que cumplen el filtro, SIN paginar. Para exportaciones completas.</summary>
    Task<List<ActividadDto>> BuscarTodasAsync(ActividadFiltro f, CancellationToken ct = default);
    Task<ActividadDto> GuardarAsync(GuardarActividadRequest req, CancellationToken ct = default);
    Task<bool> EliminarAsync(long actividadId, string? usuario, CancellationToken ct = default);
    Task<ResumenJornadaDto> ResumenAsync(ActividadFiltro f, CancellationToken ct = default);

    // Autocompletar No. Solicitud desde tickets/OC reales de mdb
    Task<IReadOnlyList<OpcionTicketDto>> BuscarTicketsAsync(string termino, CancellationToken ct = default);
    // Desglose: cuánto se ha trabajado en un ticket/OC
    Task<IReadOnlyList<ActividadDto>> ActividadesDeTicketAsync(string refNum, CancellationToken ct = default);
}
