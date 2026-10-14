namespace TicketAnalytics.Web.Models;

public class OrdenCambioFiltro
{
    public string? NoOC { get; set; }
    public string? Texto { get; set; }
    public List<string>? Estados { get; set; }
    public List<string>? Categorias { get; set; }
    public string? ComplejidadOC { get; set; }
    public string? AsignatarioUuid { get; set; }
    public string? AsignatarioNombre { get; set; }
    public DateTime? AperturaDesde { get; set; }
    public DateTime? AperturaHasta { get; set; }
    public DateTime? CierreDesde { get; set; }
    public DateTime? CierreHasta { get; set; }
    public DateTime? VencimientoDesde { get; set; }
    public DateTime? VencimientoHasta { get; set; }
    public int? MesVencimiento { get; set; }
    public int? AnioVencimiento { get; set; }
    public bool? Vencidas { get; set; }
    public bool? VencenHoy { get; set; }
    public bool? VencenEsteMes { get; set; }
    public bool? VencenProximoMes { get; set; }
    public int? VencenEnDias { get; set; }
    public bool? SinFechaVencimiento { get; set; }
    public bool? SinAnalista { get; set; }
    public bool? SoloAbiertas { get; set; }
    public bool? SoloCerradas { get; set; }
    public bool? ConTicketsRelacionados { get; set; }
    public bool? ConTicketCerradoOcPendiente { get; set; }
    public int? MinTickets { get; set; }
    public int? MaxTickets { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string OrderBy { get; set; } = "vencimiento";
    public bool OrderDesc { get; set; } = false;
}

public class OrdenCambioDashboardDto
{
    public int Total { get; set; }
    public int Abiertas { get; set; }
    public int Cerradas { get; set; }
    public int Vencidas { get; set; }
    public int VencenEsteMes { get; set; }
    public int VencenProximos7 { get; set; }
    public int SinFechaVencimiento { get; set; }
    public int SinAnalista { get; set; }
    public int ConTicketCerradoOcPendiente { get; set; }
    public double PctCompletadas { get; set; }
    public double PromedioDiasAbiertos { get; set; }
    public double PromedioDiasRestantes { get; set; }
    public List<ConteoOc> PorEstado { get; set; } = new();
    public List<ConteoOc> PorAnalista { get; set; } = new();
    public List<ConteoOc> PorCategoria { get; set; } = new();
    public List<ConteoOc> PorComplejidad { get; set; } = new();
    public List<AntiguedadOc> Antiguedad { get; set; } = new();
    public List<TendenciaMesOc> CreadasVsCerradas { get; set; } = new();
    public List<VencimientoSemanaOc> VencenPorSemana { get; set; } = new();
    public List<ConteoOc> VencidasPorAnalista { get; set; } = new();
}

public class ConteoOc { public string Etiqueta { get; set; } = ""; public int Cantidad { get; set; } }
public class AntiguedadOc { public string Rango { get; set; } = ""; public int Cantidad { get; set; } }
public class TendenciaMesOc { public string Mes { get; set; } = ""; public int Creadas { get; set; } public int Cerradas { get; set; } }
public class VencimientoSemanaOc { public string Semana { get; set; } = ""; public int Cantidad { get; set; } }

public class ControlVencimientosDto
{
    public int Vencidas { get; set; }
    public int VencenHoy { get; set; }
    public int Vencen7 { get; set; }
    public int Vencen15 { get; set; }
    public int Vencen30 { get; set; }
    public int VencenEsteMes { get; set; }
    public int VencenProximoMes { get; set; }
    public int SinFecha { get; set; }
    public List<ConteoOc> PorAnalista { get; set; } = new();
    public List<ConteoOc> PorEstado { get; set; } = new();
    public List<ConteoOc> PorCategoria { get; set; } = new();
    public List<VencimientoSemanaOc> PorSemana { get; set; } = new();
    public List<OrdenCambioConTicketsDto> VencenPrimero { get; set; } = new();
}

/// <summary>Vista "Atención por persona": una fila por analista del grupo (espejo del DTO de Application).</summary>
public class AtencionPorPersonaDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string Periodo { get; set; } = "";
    public int DiasSinAvance { get; set; }
    public DateTime GeneradoEn { get; set; }
    public List<AtencionPersonaDto> Personas { get; set; } = new();
}

public class AtencionPersonaDto
{
    public string Analista { get; set; } = "";
    public string? UserId { get; set; }
    public string? Uuid { get; set; }
    /// <summary>critico | atencion | ok</summary>
    public string Nivel { get; set; } = "ok";
    public int OcAbiertas { get; set; }
    public int OcVencidas { get; set; }
    public int OcVencenHoy { get; set; }
    public int OcVencenSemana { get; set; }
    public int OcVencenMes { get; set; }
    public int TicketsAbiertos { get; set; }
    public int TicketsSlaVencido { get; set; }
    public int TicketsSinGestion { get; set; }
    public int TicketsSinAvance { get; set; }
}
