namespace TicketAnalytics.Application.TrabajoDiario.DTOs;

public class TecnicoDto
{
    public int TecnicoId { get; set; }
    public string Nombre { get; set; } = default!;
    public bool Activo { get; set; }
    public bool EsAnalistaMdb { get; set; }
}

public class ActividadDto
{
    public long ActividadId { get; set; }
    public DateOnly Fecha { get; set; }
    public int TecnicoId { get; set; }
    public string TecnicoNombre { get; set; } = "";
    public string Actividad { get; set; } = "";
    public int Tipo { get; set; }
    public string TipoTexto { get; set; } = "";
    public string? NoSolicitud { get; set; }
    public string? RefNumVinculado { get; set; }
    public string? TipoVinculo { get; set; }
    public string? Proyecto { get; set; }
    public decimal Horas { get; set; }
    public int Estado { get; set; }
    public string EstadoTexto { get; set; } = "";
    public string? Comentario { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime CreadoEn { get; set; }
    public string? ModificadoPor { get; set; }
    public DateTime? ModificadoEn { get; set; }
}

public class GuardarActividadRequest
{
    public long? ActividadId { get; set; }      // null = crear, con valor = editar
    public DateOnly Fecha { get; set; }
    public int TecnicoId { get; set; }
    public string Actividad { get; set; } = "";
    public int Tipo { get; set; }
    public string? NoSolicitud { get; set; }
    public string? RefNumVinculado { get; set; }
    public string? TipoVinculo { get; set; }
    public string? Proyecto { get; set; }
    public decimal Horas { get; set; }
    public int Estado { get; set; }
    public string? Comentario { get; set; }
    public string? Usuario { get; set; }
}

public class ActividadFiltro
{
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public int? TecnicoId { get; set; }
    public int? Tipo { get; set; }
    public int? Estado { get; set; }
    public string? Proyecto { get; set; }
    public string? NoSolicitud { get; set; }
    public string? Texto { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string OrderBy { get; set; } = "fecha";
    public bool OrderDesc { get; set; } = true;
}

public class ResumenJornadaDto
{
    public int TotalActividades { get; set; }
    public decimal TotalHoras { get; set; }
    public int Completadas { get; set; }
    public int Pendientes { get; set; }
    public int EnProceso { get; set; }
    public int Bloqueadas { get; set; }
    public int Canceladas { get; set; }

    // KPIs ampliados
    public decimal HorasHoy { get; set; }
    public decimal HorasSemana { get; set; }
    public decimal HorasMes { get; set; }
    public decimal PromedioHorasPorActividad { get; set; }
    public decimal PromedioHorasPorTecnico { get; set; }
    public string? TecnicoMasActividades { get; set; }
    public string? TecnicoMasHoras { get; set; }
    public string? ProyectoMasDedicacion { get; set; }
    public int OrdenesCambioTrabajadas { get; set; }
    public int IncidentesTrabajados { get; set; }
    public double PctCompletadas { get; set; }

    // Comparación con período anterior (mismo tamaño de ventana, inmediatamente antes)
    public int TotalActividadesAnterior { get; set; }
    public decimal TotalHorasAnterior { get; set; }

    public List<CargaTecnicoDto> PorTecnico { get; set; } = new();
    public List<ConteoDto> PorTipo { get; set; } = new();
    public List<ConteoDto> PorEstado { get; set; } = new();
    public List<HorasDiaDto> HorasPorDia { get; set; } = new();

    // Gráficos nuevos
    public List<ConteoDto> ActividadesPorTecnico { get; set; } = new();
    public List<HorasPeriodoDto> HorasPorSemana { get; set; } = new();
    public List<HorasPeriodoDto> HorasPorMes { get; set; } = new();
    public List<ConteoDto> HorasPorProyecto { get; set; } = new();
    public List<ConteoDto> ActividadesPorProyecto { get; set; } = new();
    public List<CompletadasVsPendientesDto> CompletadasVsPendientes { get; set; } = new();
    public List<ConteoDto> TicketsPorTecnico { get; set; } = new();
}

public class HorasPeriodoDto { public string Periodo { get; set; } = ""; public decimal Horas { get; set; } }
public class CompletadasVsPendientesDto { public string Tecnico { get; set; } = ""; public int Completadas { get; set; } public int Pendientes { get; set; } }

public class CargaTecnicoDto
{
    public string Tecnico { get; set; } = "";
    public decimal Horas { get; set; }
    public int Completadas { get; set; }
    public int PendientesEnProceso { get; set; }
    public int Bloqueadas { get; set; }
    public int Activas { get; set; }
    public int TotalActividades { get; set; }
    public double PctDelTotalHoras { get; set; }
    public double PctFinalizacion { get; set; }
    public decimal PromedioHorasPorActividad { get; set; }
    public string? ProyectoPrincipal { get; set; }
    public DateOnly? UltimaActividad { get; set; }
    public string NivelCarga { get; set; } = "media";  // alta | media | baja
}

public class ConteoDto { public string Etiqueta { get; set; } = ""; public int Cantidad { get; set; } public decimal Horas { get; set; } }
public class HorasDiaDto { public DateOnly Dia { get; set; } public decimal Horas { get; set; } }

public class OpcionTicketDto
{
    public string RefNum { get; set; } = "";
    public string TipoVinculo { get; set; } = "cr";   // cr | chg
    public string? Descripcion { get; set; }
    public string Display => $"{RefNum} · {(TipoVinculo == "chg" ? "OC" : "Ticket")}" + (string.IsNullOrEmpty(Descripcion) ? "" : $" — {Descripcion}");
}
