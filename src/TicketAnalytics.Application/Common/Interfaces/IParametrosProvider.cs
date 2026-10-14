using TicketAnalytics.Application.Common.Models;

namespace TicketAnalytics.Application.Common.Interfaces;

/// <summary>Parámetros de negocio desde la base local, con caché en memoria.</summary>
public interface IParametrosProvider
{
    Task<ParametrosNegocio> ObtenerAsync(CancellationToken ct = default);

    /// <summary>Último valor cargado (o los valores de respaldo). Para consumidores síncronos.</summary>
    ParametrosNegocio Ultimos { get; }

    /// <summary>Descarta la caché: el próximo ObtenerAsync vuelve a leer la base.</summary>
    void Invalidar();
}

/// <summary>Grupo de analistas del área: userid (base local) resuelto a nombre y UUID (MDB), con caché.</summary>
public interface IGrupoAnalistasProvider
{
    Task<IReadOnlyList<MiembroGrupo>> ObtenerAsync(CancellationToken ct = default);
    void Invalidar();
}
