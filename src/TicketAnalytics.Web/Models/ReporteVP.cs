namespace TicketAnalytics.Web.Models;

public class ReporteVPFiltro
{
    public string? Periodo { get; set; }
    public int? CategoriaId { get; set; }
    public int? Estado { get; set; }
    public string? Responsable { get; set; }
    public string? Texto { get; set; }
    public bool? SoloConProveedor { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;

    public ReporteVPFiltro Clonar() => new()
    {
        Periodo = Periodo, CategoriaId = CategoriaId, Estado = Estado,
        Responsable = Responsable, Texto = Texto, SoloConProveedor = SoloConProveedor,
        Page = Page, PageSize = PageSize
    };
}

public class CasoReporteVPDto
{
    public long SeguimientoId { get; set; }
    public string RefNum { get; set; } = "";
    public string TipoOrigen { get; set; } = "cr";
    public string TipoTexto { get; set; } = "";
    public string? Resumen { get; set; }
    public string? CategoriaOrigen { get; set; }
    public int? CategoriaVPId { get; set; }
    public string CategoriaVP { get; set; } = "Sin categoría";
    public string? CategoriaColor { get; set; }
    public int CategoriaOrden { get; set; }
    public int Estado { get; set; }
    public string EstadoTexto { get; set; } = "";
    public string EstadoColor { get; set; } = "";
    public string EstadoGrupo { get; set; } = "";
    public string? Responsable { get; set; }
    public int ReportadoProveedor { get; set; }
    public string ReportadoProveedorTexto { get; set; } = "";
    public string? Proveedor { get; set; }
    public string? NotaEjecutiva { get; set; }
    public string? Jira { get; set; }
    public DateTime? UltimaActualizacion { get; set; }
    public DateOnly? Periodo { get; set; }
}

public class CategoriaVPResumenDto
{
    public int? CategoriaId { get; set; }
    public string Categoria { get; set; } = "";
    public string? Color { get; set; }
    public int Orden { get; set; }
    public int Total { get; set; }
    public Dictionary<string, int> PorEstado { get; set; } = new();
    public List<CasoReporteVPDto> Casos { get; set; } = new();
}

public class ConteoEstadoVPDto
{
    public int Estado { get; set; }
    public string Nombre { get; set; } = "";
    public string Color { get; set; } = "";
    public int Cantidad { get; set; }
    public double Porcentaje { get; set; }
}

public class PuntoTendenciaVPDto
{
    public string Periodo { get; set; } = "";
    public string Etiqueta { get; set; } = "";
    public int Total { get; set; }
    public int Completados { get; set; }
}

public class KpiVPDto
{
    public string Clave { get; set; } = "";
    public string Titulo { get; set; } = "";
    public int Valor { get; set; }
    public int? ValorAnterior { get; set; }
    public int? Delta { get; set; }
    public double? DeltaPct { get; set; }
}

public class ReporteVPOverviewDto
{
    public string Periodo { get; set; } = "";
    public string PeriodoTexto { get; set; } = "";
    public string? PeriodoAnteriorTexto { get; set; }
    public DateTime GeneradoEn { get; set; }
    public List<KpiVPDto> Kpis { get; set; } = new();
    public List<CategoriaVPResumenDto> Categorias { get; set; } = new();
    public List<ConteoEstadoVPDto> PorEstado { get; set; } = new();
    public List<PuntoTendenciaVPDto> Tendencia { get; set; } = new();
    public List<CasoReporteVPDto> Casos { get; set; } = new();
    public int TotalCasos { get; set; }
    public string ResumenEjecutivo { get; set; } = "";
}

public class GuardarCasoVPRequest
{
    public string RefNum { get; set; } = "";
    public string TipoOrigen { get; set; } = "cr";
    public string? Periodo { get; set; }
    public int? CategoriaId { get; set; }
    public int Estado { get; set; }
    public string? Responsable { get; set; }
    public string? NotaEjecutiva { get; set; }
    public string? Usuario { get; set; }
}

public class CategoriaVPDto
{
    public int CategoriaId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public string? Color { get; set; }
}

/// <summary>Estados del reporte VP: misma fuente de verdad que el backend.</summary>
public static class EstadosVP
{
    public record Opcion(int Valor, string Nombre, string Color);

    public static readonly Opcion[] Todos =
    {
        new(4, "Completado",  "#2E7D32"),
        new(3, "Producción",  "#1976D2"),
        new(2, "QA",          "#ED6C02"),
        new(1, "Desarrollo",  "#6B4EFF"),
        new(0, "Pendiente",   "#667085"),
        new(5, "Bloqueado",   "#C62828")
    };

    public static string Nombre(int v) => Todos.FirstOrDefault(x => x.Valor == v)?.Nombre ?? "Pendiente";
    public static string Color(int v) => Todos.FirstOrDefault(x => x.Valor == v)?.Color ?? "#667085";
}

/// <summary>Estado de reporte a proveedor del caso (No / Sí / Pendiente).</summary>
public static class ReportadoProveedorOpciones
{
    public record Opcion(int Valor, string Nombre);

    public static readonly Opcion[] Todos =
    {
        new(0, "No"),
        new(2, "Pendiente"),
        new(1, "Sí")
    };

    public static string Nombre(int v) => Todos.FirstOrDefault(x => x.Valor == v)?.Nombre ?? "Pendiente";
}

/// <summary>Ciclo de vida de un reporte de proveedor YA creado.
/// NO confundir con ReportadoProveedorOpciones (que solo dice si el caso va al proveedor).</summary>
public static class EstadosReporteProveedor
{
    public record Opcion(int Valor, string Nombre);

    public static readonly Opcion[] Todos =
    {
        new(0, "Abierto"),
        new(1, "En espera de respuesta"),
        new(2, "Respuesta recibida"),
        new(3, "En seguimiento"),
        new(4, "Resuelto"),
        new(5, "Cerrado")
    };

    public static string Nombre(int v) => Todos.FirstOrDefault(x => x.Valor == v)?.Nombre ?? "Abierto";
}

/// <summary>Opción de un catálogo estructurado (valor + nombre para mostrar).</summary>
public class OpcionCatalogo
{
    public int Valor { get; set; }
    public string Nombre { get; set; } = "";
}

/// <summary>Sistema como opción: trae su proveedor para la selección encadenada.</summary>
public class SistemaOpcion : OpcionCatalogo
{
    public int? ProveedorId { get; set; }
    public string? Proveedor { get; set; }
    public string? Color { get; set; }
    public bool AplicaSubtipoActualizacion { get; set; }
}

/// <summary>Catálogos de gestión en una sola respuesta.</summary>
public class CatalogosGestionDto
{
    public List<SistemaOpcion> Sistemas { get; set; } = new();
    public List<OpcionCatalogo> Proveedores { get; set; } = new();
    public List<OpcionCatalogo> TiposSolucion { get; set; } = new();
    public List<OpcionCatalogo> TiposActualizacion { get; set; } = new();
    public List<OpcionCatalogo> TiposCorreccion { get; set; } = new();
}

public class ConteoClasificacionDto
{
    public int Valor { get; set; }
    public string Nombre { get; set; } = "";
    public int Cantidad { get; set; }
    public double Porcentaje { get; set; }
    public string? Color { get; set; }
    public string? Detalle { get; set; }
}

/// <summary>Un sistema con el desglose de las soluciones de sus casos.</summary>
public class SistemaSolucionDto
{
    public int SistemaId { get; set; }
    public string Sistema { get; set; } = "";
    public string? Proveedor { get; set; }
    public string? Color { get; set; }
    public int Total { get; set; }
    public int SinSolucion { get; set; }
    public List<ConteoClasificacionDto> Soluciones { get; set; } = new();
}

/// <summary>Entradas contra cierres de un mes, para tickets y OC.</summary>
public class FlujoMensualDto
{
    public string Periodo { get; set; } = "";
    public int TicketsEntraron { get; set; }
    public int TicketsCerrados { get; set; }
    public int OcEntraron { get; set; }
    public int OcCerradas { get; set; }
}

/// <summary>Caso pendiente de reportar / en espera del proveedor.</summary>
public class CasoProveedorDto
{
    public long SeguimientoId { get; set; }
    public string RefNum { get; set; } = "";
    public string TipoOrigen { get; set; } = "cr";
    public string? Sistema { get; set; }
    public string? Proveedor { get; set; }
    public string? Tecnico { get; set; }
    public string Etapa { get; set; } = "";
    public string? TicketProveedor { get; set; }
    public DateTime Desde { get; set; }
    public int Dias { get; set; }
}

/// <summary>Sistema del catálogo (administración).</summary>
public class SistemaDto
{
    public int SistemaId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public int? ProveedorId { get; set; }
    public string? Proveedor { get; set; }
    public int Criticidad { get; set; }
    public bool Activo { get; set; } = true;
    public int Orden { get; set; }
    public string? Color { get; set; }
    public bool AplicaSubtipoActualizacion { get; set; }
    public int CasosClasificados { get; set; }
}

public class GuardarSistemaRequest
{
    public int? SistemaId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public int? ProveedorId { get; set; }
    public int Criticidad { get; set; }
    public bool Activo { get; set; } = true;
    public int Orden { get; set; }
    public string? Color { get; set; }
    public bool AplicaSubtipoActualizacion { get; set; }
}

public class TipoCorreccionDto
{
    public int TipoCorreccionId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public int Orden { get; set; }
    public int Usos { get; set; }
}

public class GuardarTipoCorreccionRequest
{
    public int? TipoCorreccionId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public int Orden { get; set; }
}

public class ResultadoEliminacionDto
{
    public bool Eliminado { get; set; }
    public bool Desactivado { get; set; }
    public string Mensaje { get; set; } = "";
}

/// <summary>Niveles de criticidad de un sistema (0..3).</summary>
public static class CriticidadSistema
{
    public static readonly (int Valor, string Nombre)[] Todos = { (0, "Sin definir"), (1, "Baja"), (2, "Media"), (3, "Alta") };
    public static string Nombre(int v) => Todos.FirstOrDefault(x => x.Valor == v).Nombre ?? "Sin definir";
}

/// <summary>Indicadores derivados de la clasificación estructurada.</summary>
public class IndicadoresGestionDto
{
    public int TotalGestionados { get; set; }
    public List<ConteoClasificacionDto> PorSistema { get; set; } = new();
    public List<ConteoClasificacionDto> PorTipoSolucion { get; set; } = new();
    public List<ConteoClasificacionDto> PorTipoCorreccion { get; set; } = new();
    public List<ConteoClasificacionDto> PorCausaRaiz { get; set; } = new();
    public List<SistemaSolucionDto> SistemasConSolucion { get; set; } = new();
    public int SinSistema { get; set; }
    public List<ConteoClasificacionDto> PorTipoActualizacionDatos { get; set; } = new();
    public int TotalActualizacionesDatos { get; set; }
    public int ReportadosProveedor { get; set; }
    public int PendientesReportarProveedor { get; set; }
    public int NoRequierenProveedor { get; set; }
    public int ConCausaRaiz { get; set; }
    public int SinCausaRaiz { get; set; }
    public int ConJira { get; set; }
    public int ReportadosVP { get; set; }
    public List<ConteoClasificacionDto> TopSistemas { get; set; } = new();
    public List<ConteoClasificacionDto> TopTiposSolucion { get; set; } = new();
}

/// <summary>Caso que todavía requiere escalamiento a proveedor (No o Pendiente).</summary>
public class PendienteEscalarDto
{
    public long SeguimientoId { get; set; }
    public string RefNum { get; set; } = "";
    public string TipoOrigen { get; set; } = "cr";
    public string? Categoria { get; set; }
    public string? TecnicoResponsable { get; set; }
    public int Estado { get; set; }
    public string EstadoTexto { get; set; } = "";
    public string? ProximosPasos { get; set; }
    public DateTime ActualizadoEn { get; set; }
}
