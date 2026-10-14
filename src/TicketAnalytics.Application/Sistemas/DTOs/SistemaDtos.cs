namespace TicketAnalytics.Application.Sistemas.DTOs;

/// <summary>Sistema del catálogo con su proveedor responsable (N:1).</summary>
public class SistemaDto
{
    public int SistemaId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public int? ProveedorId { get; set; }
    /// <summary>Nombre del proveedor; null = por asignar.</summary>
    public string? Proveedor { get; set; }
    public int Criticidad { get; set; }
    public bool Activo { get; set; }
    public int Orden { get; set; }
    public string? Color { get; set; }
    public bool AplicaSubtipoActualizacion { get; set; }
    /// <summary>Casos de seguimiento clasificados con este sistema (para avisar antes de borrar).</summary>
    public int CasosClasificados { get; set; }
}

public class GuardarSistemaRequest
{
    public int? SistemaId { get; set; }          // null = crear
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
    public bool Activo { get; set; }
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

/// <summary>Opción simple de un selector.</summary>
public class OpcionDto
{
    public int Valor { get; set; }
    public string Nombre { get; set; } = "";
}

/// <summary>Sistema como opción del formulario: trae su proveedor para la selección encadenada.</summary>
public class SistemaOpcionDto : OpcionDto
{
    public int? ProveedorId { get; set; }
    public string? Proveedor { get; set; }
    public string? Color { get; set; }
    public bool AplicaSubtipoActualizacion { get; set; }
}

/// <summary>Todos los catálogos del formulario de gestión en una sola respuesta.</summary>
public class CatalogosGestionDto
{
    public List<SistemaOpcionDto> Sistemas { get; set; } = new();
    public List<OpcionDto> Proveedores { get; set; } = new();
    public List<OpcionDto> TiposSolucion { get; set; } = new();
    public List<OpcionDto> TiposActualizacion { get; set; } = new();
    public List<OpcionDto> TiposCorreccion { get; set; } = new();
}

/// <summary>Resultado de borrar un elemento de catálogo: si tenía uso, solo se desactiva.</summary>
public class ResultadoEliminacionDto
{
    public bool Eliminado { get; set; }
    public bool Desactivado { get; set; }
    public string Mensaje { get; set; } = "";
}
