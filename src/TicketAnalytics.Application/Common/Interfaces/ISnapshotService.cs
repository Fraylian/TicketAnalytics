namespace TicketAnalytics.Application.Common.Interfaces;

public interface ISnapshotService
{
    Task<int> CapturarYDetectarCambiosAsync(CancellationToken ct = default);
}
