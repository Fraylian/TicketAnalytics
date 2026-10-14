namespace TicketAnalytics.Domain.Entities;

/// <summary>
/// Parámetro de negocio administrable (base LOCAL). Reemplaza los valores que antes estaban
/// quemados en el código: fecha base del histórico, días de vencimiento de OC por complejidad,
/// umbrales de alerta, etc. Se cambian sin recompilar (tabla dbo.ParametroSistema).
/// </summary>
public class ParametroSistema
{
    public string Clave { get; set; } = default!;
    public string Valor { get; set; } = default!;
    public string? Descripcion { get; set; }
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    public string? ActualizadoPor { get; set; }
}

/// <summary>
/// Miembro del grupo de analistas del área (base LOCAL). Se identifica por su userid de
/// ca_contact; el nombre y el UUID se resuelven contra MDB. Reemplaza la lista fija en código.
/// </summary>
public class AnalistaGrupo
{
    public string UserId { get; set; } = default!;
    public bool Activo { get; set; } = true;
    public int Orden { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
