using TicketAnalytics.Domain.Enums;

namespace TicketAnalytics.Application.ReporteVP.DTOs;

/// <summary>Filtro del reporte mensual al VP.</summary>
public class ReporteVPFiltro
{
    /// <summary>Período en formato yyyy-MM (ej. "2026-08"). Si viene vacío se usa el mes actual.</summary>
    public string? Periodo { get; set; }
    public int? CategoriaId { get; set; }
    public int? Estado { get; set; }               // EstadoReporteVP
    public string? Responsable { get; set; }
    public string? Texto { get; set; }
    public bool? SoloConProveedor { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;

    /// <summary>Primer día del mes del período (o del mes actual si no se especificó).</summary>
    public DateOnly PeriodoInicio()
    {
        if (!string.IsNullOrWhiteSpace(Periodo)
            && DateOnly.TryParse($"{Periodo}-01", out var d)) return d;
        var hoy = DateTime.Today;
        return new DateOnly(hoy.Year, hoy.Month, 1);
    }
}

/// <summary>Un caso tal como aparece en el reporte del VP.</summary>
public class CasoReporteVPDto
{
    public long SeguimientoId { get; set; }
    public string RefNum { get; set; } = "";
    public string TipoOrigen { get; set; } = "cr";      // cr | chg
    public string TipoTexto => TipoOrigen == "chg" ? "Orden de Cambio" : "Ticket";
    public string? Resumen { get; set; }
    public string? CategoriaOrigen { get; set; }        // categoría del sistema (CA SDM)
    public int? CategoriaVPId { get; set; }
    public string CategoriaVP { get; set; } = "Sin categoría";
    public string? CategoriaColor { get; set; }
    public int CategoriaOrden { get; set; }
    public EstadoReporteVP Estado { get; set; }
    public string EstadoTexto { get; set; } = "";
    public string EstadoColor { get; set; } = "";
    public string EstadoGrupo { get; set; } = "";       // Completado | En proceso | Pendiente
    public string? Responsable { get; set; }
    public EstadoReporteProveedorTicket ReportadoProveedor { get; set; }
    public string ReportadoProveedorTexto { get; set; } = "";
    public string? Proveedor { get; set; }
    public string? NotaEjecutiva { get; set; }
    public string? Jira { get; set; }
    public DateTime? UltimaActualizacion { get; set; }
    public DateOnly? Periodo { get; set; }
}

/// <summary>Conteo por categoría con su desglose de estados (barra apilada).</summary>
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
    public string Periodo { get; set; } = "";      // yyyy-MM
    public string Etiqueta { get; set; } = "";     // "Ago 2026"
    public int Total { get; set; }
    public int Completados { get; set; }
}

/// <summary>KPI con comparación contra el período anterior.</summary>
public class KpiVPDto
{
    public string Clave { get; set; } = "";
    public string Titulo { get; set; } = "";
    public int Valor { get; set; }
    public int? ValorAnterior { get; set; }
    public int? Delta => ValorAnterior.HasValue ? Valor - ValorAnterior.Value : null;
    public double? DeltaPct => (ValorAnterior is > 0) ? Math.Round((Valor - ValorAnterior.Value) * 100.0 / ValorAnterior.Value, 1) : null;
}

/// <summary>Payload completo de la pantalla (un solo round trip).</summary>
public class ReporteVPOverviewDto
{
    public string Periodo { get; set; } = "";
    public string PeriodoTexto { get; set; } = "";
    public string? PeriodoAnteriorTexto { get; set; }
    public DateTime GeneradoEn { get; set; } = DateTime.Now;
    public List<KpiVPDto> Kpis { get; set; } = new();
    public List<CategoriaVPResumenDto> Categorias { get; set; } = new();
    public List<ConteoEstadoVPDto> PorEstado { get; set; } = new();
    public List<PuntoTendenciaVPDto> Tendencia { get; set; } = new();
    public List<CasoReporteVPDto> Casos { get; set; } = new();
    public int TotalCasos { get; set; }
    /// <summary>Resumen narrativo generado con reglas simples desde los propios datos.</summary>
    public string ResumenEjecutivo { get; set; } = "";
}

/// <summary>Alta/edición de un caso dentro del reporte VP.</summary>
public class GuardarCasoVPRequest
{
    public string RefNum { get; set; } = "";
    public string TipoOrigen { get; set; } = "cr";
    public string? Periodo { get; set; }               // yyyy-MM
    public int? CategoriaId { get; set; }
    public int Estado { get; set; }                    // EstadoReporteVP
    public string? Responsable { get; set; }
    public string? NotaEjecutiva { get; set; }
    public string? Usuario { get; set; }
}

/// <summary>Categoría del catálogo (para selectores y administración).</summary>
public class CategoriaVPDto
{
    public int CategoriaId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public string? Color { get; set; }
}
