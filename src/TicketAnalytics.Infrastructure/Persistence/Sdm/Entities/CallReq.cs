namespace TicketAnalytics.Infrastructure.Persistence.Sdm.Entities;

/// <summary>[MDB].dbo.call_req. SOLO LECTURA. Se mapea la tabla directa (no View_Request)
/// para no perder tickets sin priority/customer validos.</summary>
public class CallReq
{
    public int id { get; set; }
    public string persid { get; set; } = default!;
    public string ref_num { get; set; } = default!;
    public string? summary { get; set; }
    public string? description { get; set; }
    public string? status { get; set; }
    public int active_flag { get; set; }
    public int? open_date { get; set; }
    public int? close_date { get; set; }
    public int? resolve_date { get; set; }
    public byte[]? assignee { get; set; }
    public byte[]? group_id { get; set; }
    public byte[]? customer { get; set; }
    public string? category { get; set; }
    public int? sla_violation { get; set; }
    public int? change { get; set; }
    public byte[]? tenant { get; set; }
}
