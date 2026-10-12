namespace TicketAnalytics.Infrastructure.Persistence.Sdm.Entities;

/// <summary>[MDB].dbo.chg (Ordenes de Cambio). SOLO LECTURA.</summary>
public class Chg
{
    public int id { get; set; }
    public string persid { get; set; } = default!;
    public string chg_ref_num { get; set; } = default!;
    public string? summary { get; set; }
    public string? description { get; set; }
    public string? status { get; set; }
    public int active_flag { get; set; }
    public int? open_date { get; set; }
    public int? close_date { get; set; }
    /// <summary>Fecha planificada de fin de la OC (vencimiento). Epoch RD.</summary>
    public int? sched_end_date { get; set; }
    /// <summary>Fecha planificada de inicio. Epoch RD.</summary>
    public int? sched_start_date { get; set; }
    /// <summary>Fecha requerida (need-by). Epoch RD. Respaldo si sched_end_date es nulo.</summary>
    public int? need_by { get; set; }
    public byte[]? assignee { get; set; }
    public byte[]? group_id { get; set; }
    public string? category { get; set; }
    public int? sla_violation { get; set; }
    public byte[]? tenant { get; set; }
}
