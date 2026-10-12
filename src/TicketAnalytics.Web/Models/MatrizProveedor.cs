namespace TicketAnalytics.Web.Models;

public static class SlaProveedorCatalogo
{
    public static readonly string[] Severidades = { "Critico", "Alto", "Medio", "Bajo" };
}

public class ProveedorCatalogoDto
{
    public int ProveedorId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Contacto { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public bool Activo { get; set; } = true;
    public int? SlaCriticoHoras { get; set; }
    public int? SlaAltoHoras { get; set; }
    public int? SlaMedioHoras { get; set; }
    public int? SlaBajoHoras { get; set; }

    public int? SlaPara(string severidad) => severidad?.ToLowerInvariant() switch
    {
        "critico" => SlaCriticoHoras,
        "alto" => SlaAltoHoras,
        "medio" => SlaMedioHoras,
        "bajo" => SlaBajoHoras,
        _ => SlaMedioHoras
    };
}

public class GuardarProveedorRequest
{
    public int? ProveedorId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Contacto { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public bool Activo { get; set; } = true;
    public int? SlaCriticoHoras { get; set; }
    public int? SlaAltoHoras { get; set; }
    public int? SlaMedioHoras { get; set; }
    public int? SlaBajoHoras { get; set; }
}

public class ReporteProveedorMatrizDto
{
    public long ReporteId { get; set; }
    public int ProveedorId { get; set; }
    public string Proveedor { get; set; } = "";
    public string? RefNumSistema { get; set; }
    public string? TipoVinculo { get; set; }
    public string? NumeroCasoExterno { get; set; }
    public string? Titulo { get; set; }
    public string? Descripcion { get; set; }
    public DateTime FechaReporte { get; set; }
    public string? ContextoEnviado { get; set; }
    public string? RespuestasRecibidas { get; set; }
    public string Urgencia { get; set; } = "Medio";
    public int? SlaHorasRespuesta { get; set; }
    public DateTime? FechaPrimeraRespuesta { get; set; }
    public bool? BrindoAsistencia { get; set; }
    public bool? EntregoCambio { get; set; }
    public bool? CumplioSla { get; set; }
    public int Estado { get; set; }
    public string EstadoTexto { get; set; } = "";
    public string? ResponsableInterno { get; set; }
    public string? ResultadoSolucion { get; set; }
    public DateTime CreadoEn { get; set; }
    public int? SlaReferenciaHoras { get; set; }
    public double? HorasRespuesta { get; set; }
}

public class GuardarReporteProveedorRequest
{
    public long? ReporteId { get; set; }
    public int ProveedorId { get; set; }
    public string? RefNumSistema { get; set; }
    public string? TipoVinculo { get; set; }
    public string? NumeroCasoExterno { get; set; }
    public string? Titulo { get; set; }
    public string? Descripcion { get; set; }
    public DateTime FechaReporte { get; set; } = DateTime.Today;
    public string? ContextoEnviado { get; set; }
    public string? RespuestasRecibidas { get; set; }
    public string Urgencia { get; set; } = "Medio";
    public int? SlaHorasRespuesta { get; set; }
    public DateTime? FechaPrimeraRespuesta { get; set; }
    public bool? BrindoAsistencia { get; set; }
    public bool? EntregoCambio { get; set; }
    public bool? CumplioSla { get; set; }
    public int Estado { get; set; }
    public string? ResponsableInterno { get; set; }
    public string? ResultadoSolucion { get; set; }
    public string? Usuario { get; set; }
}

public class ReporteProveedorFiltro
{
    public int? Mes { get; set; }
    public int? Anio { get; set; }
    public int? ProveedorId { get; set; }
    public string? ResponsableInterno { get; set; }
    public int? Estado { get; set; }
    public string? Urgencia { get; set; }
    public string? Texto { get; set; }
    public bool? SoloIncumplidosSla { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class MatrizProveedorResumenDto
{
    public int TotalReportes { get; set; }
    public int Abiertos { get; set; }
    public int Cerrados { get; set; }
    public int ConAsistencia { get; set; }
    public int SinAsistencia { get; set; }
    public int SlaCumplidos { get; set; }
    public int SlaIncumplidos { get; set; }
    public int SlaSinEvaluar { get; set; }
    public double PctCumplimientoSla { get; set; }
    public List<ConteoProveedor> PorProveedor { get; set; } = new();
    public List<ConteoProveedor> PorResponsable { get; set; } = new();
    public List<ConteoProveedor> PorSeveridad { get; set; } = new();
    public List<ConteoProveedor> PorEstado { get; set; } = new();
    public List<FilaMatrizControl> Control { get; set; } = new();
}

public class FilaMatrizControl
{
    public string Severidad { get; set; } = "";
    public string SlaTexto { get; set; } = "";
    public int CantidadSoportes { get; set; }
    public int ResueltosDentroSla { get; set; }
    public double PctCumplimiento { get; set; }
}

public class ConteoProveedor { public string Etiqueta { get; set; } = ""; public int Cantidad { get; set; } }
