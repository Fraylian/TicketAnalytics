namespace TicketAnalytics.Domain.Entities;

/// <summary>Foto del estado de un ticket en alcance. Permite detectar cambios
/// sin escribir en MDB. Almacenado en la base LOCAL.</summary>
public class TicketSnapshot
{
    public long SnapshotId { get; set; }
    public int CrId { get; set; }
    public string RefNum { get; set; } = default!;
    public string Persid { get; set; } = default!;
    public string Categoria { get; set; } = default!;
    public string? Estado { get; set; }
    public byte[]? AssigneeUuid { get; set; }
    public byte[]? GroupUuid { get; set; }
    public int? OpenDateEpoch { get; set; }
    public int? CloseDateEpoch { get; set; }
    public int? SlaViolation { get; set; }
    public int? ChangeId { get; set; }
    public DateTime CapturadoEn { get; set; } = DateTime.UtcNow;
    public byte[]? HashEstado { get; set; }
}
