namespace TicketAnalytics.Application.Tickets.DTOs;

/// <summary>
/// Vista unificada: una orden de cambio con TODOS sus tickets relacionados,
/// obtenida desde el mismo contexto de MDB en una sola consulta.
/// Reemplaza el antiguo flujo donde OC y tickets venian como Excel separados.
/// </summary>
public class OrdenCambioConTicketsDto
{
    // --- Orden de cambio ---
    public int ChgId { get; set; }
    public string NoOC { get; set; } = default!;
    public string? ResumenOC { get; set; }
    public string? DescripcionOC { get; set; }
    public string? EstadoOC { get; set; }
    public string? CategoriaOC { get; set; }
    public string? AsignatarioOC { get; set; }
    public string? GrupoOC { get; set; }
    public DateTime? FechaAperturaOC { get; set; }
    public DateTime? FechaCierreOC { get; set; }
    /// <summary>Vencimiento planificado (chg.sched_end_date). Null = sin fecha.</summary>
    public DateTime? FechaVencimientoOC { get; set; }
    public string? ComplejidadOC { get; set; }
    public string? OCResueltoDentroSLA { get; set; }

    // --- Vencimiento calculado (en memoria, no en BD) ---
    /// <summary>Días entre hoy y el vencimiento. Negativo = vencida.</summary>
    public int? DiasRestantes { get; set; }
    /// <summary>% de tiempo consumido entre apertura y vencimiento (0-100+).</summary>
    public double? PctTiempoConsumido { get; set; }
    /// <summary>vencida | vence_hoy | vence_pronto | en_curso | completada | sin_fecha</summary>
    public string EstadoVencimiento { get; set; } = "sin_fecha";
    public bool SinAnalista { get; set; }
    public bool TieneTicketCerradoOcPendiente { get; set; }
    /// <summary>Última gestión registrada en el seguimiento local (si existe).</summary>
    public DateTime? UltimaGestion { get; set; }
    public int? DiasDesdeUltimaGestion { get; set; }

    // --- Tickets relacionados (todos los call_req cuyo change apunta a esta OC) ---
    public List<TicketRelacionadoDto> Tickets { get; set; } = new();
    public int CantidadTickets => Tickets.Count;
}

public class TicketRelacionadoDto
{
    public string TipoCaso { get; set; } = default!;
    public string NoTicket { get; set; } = default!;
    public string? Categoria { get; set; }
    public string? Estado { get; set; }
    public string? Asignatario { get; set; }
    public string? UsuarioFinalAfectado { get; set; }
    public DateTime? FechaApertura { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string ResueltoDentroSLA { get; set; } = "N/A";
    public bool CerradoConCambioPendiente { get; set; }
}

/// <summary>Resumen de OC por asignatario/técnico con sus conteos por estado de vencimiento.
/// Alimenta el apartado "Fulano: N OC por vencer este mes".</summary>
public class ResumenOcPorTecnicoDto
{
    public string Tecnico { get; set; } = "";
    public int Total { get; set; }
    public int Vencidas { get; set; }        // abiertas, ya pasó el SLA
    public int VenceHoy { get; set; }
    public int VencenEsteMes { get; set; }   // abiertas, vencen dentro del mes consultado
    public int VencenPronto { get; set; }    // abiertas, <= 7 días
    public int EnCurso { get; set; }         // abiertas, vencen a futuro
    public int Completadas { get; set; }     // cerradas
    public int SinFecha { get; set; }        // sin SLA medible
    public int Abiertas => Total - Completadas;
}

/// <summary>Contenedor del tablero de OC: los apartados por vencimiento + el resumen por técnico.</summary>
public class TableroOcDto
{
    public int Total { get; set; }
    public int Vencidas { get; set; }
    public int VenceHoy { get; set; }
    public int VencenEsteMes { get; set; }
    public int VencenPronto { get; set; }
    public int EnCurso { get; set; }
    public int Completadas { get; set; }
    public int SinFecha { get; set; }
    public List<ResumenOcPorTecnicoDto> PorTecnico { get; set; } = new();
    public List<OrdenCambioConTicketsDto> Ordenes { get; set; } = new();
}

/// <summary>Analista del grupo resuelto desde la base (para selectores de responsable).</summary>
public class AnalistaGrupoDto
{
    public string UserId { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Uuid { get; set; }   // hex del contact_uuid, para filtrar por assignee
}
