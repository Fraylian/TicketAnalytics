using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TicketAnalytics.Domain.Common;

namespace TicketAnalytics.Infrastructure.Persistence.Converters;

public sealed class EpochRdConverter : ValueConverter<DateTime?, int?>
{
    public EpochRdConverter() : base(
        dt => dt.HasValue ? EpochRD.ToEpoch(dt.Value) : (int?)null,
        epoch => EpochRD.ToDateTime(epoch))
    { }
}
