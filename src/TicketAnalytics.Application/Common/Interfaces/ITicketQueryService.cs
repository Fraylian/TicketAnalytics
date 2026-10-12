using TicketAnalytics.Application.Common.Models;
using TicketAnalytics.Application.Tickets.DTOs;

namespace TicketAnalytics.Application.Common.Interfaces;

public interface ITicketQueryService
{
    Task<PagedResult<TicketEnriquecidoDto>> BuscarAsync(TicketFiltro filtro, CancellationToken ct = default);
    Task<TicketEnriquecidoDto?> ObtenerPorRefNumAsync(string refNum, CancellationToken ct = default);

    /// <summary>Todos los tickets del filtro SIN paginar. Para exportaciones completas.</summary>
    Task<List<TicketEnriquecidoDto>> BuscarTodosAsync(TicketFiltro filtro, CancellationToken ct = default);

    /// <summary>Trae una OC con TODOS sus tickets relacionados desde el mismo contexto MDB.</summary>
    Task<OrdenCambioConTicketsDto?> ObtenerOrdenCambioConTicketsAsync(string chgRefNum, CancellationToken ct = default);

    /// <summary>Lista OCs (con sus tickets) del alcance, aplicando filtros.</summary>
    Task<PagedResult<OrdenCambioConTicketsDto>> BuscarOrdenesCambioAsync(TicketFiltro filtro, CancellationToken ct = default);

    /// <summary>Tablero de OC: apartados por vencimiento y resumen por técnico, para el filtro dado.</summary>
    Task<TableroOcDto> TableroOrdenesCambioAsync(TicketFiltro filtro, CancellationToken ct = default);

    /// <summary>Resuelve los analistas del grupo desde la base (userid -> nombre + uuid hex),
    /// para poblar los selectores de responsable en toda la app.</summary>
    Task<IReadOnlyList<AnalistaGrupoDto>> AnalistasDelGrupoAsync(CancellationToken ct = default);

    /// <summary>Bandeja de tickets abiertos por analista del grupo, con estado de vencimiento
    /// (por vencer / vencidos) para alertas. Muestra qué tiene cada quien.</summary>
    Task<BandejaAnalistasDto> BandejaTicketsPorAnalistaAsync(TicketFiltro filtro, CancellationToken ct = default);
}
