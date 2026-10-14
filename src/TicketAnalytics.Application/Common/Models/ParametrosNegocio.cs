using TicketAnalytics.Domain.Enums;

namespace TicketAnalytics.Application.Common.Models;

/// <summary>
/// Parámetros de negocio ya tipados (leídos de dbo.ParametroSistema en la base local).
/// Los valores por defecto de este record son SOLO el respaldo si la tabla no responde;
/// la fuente de verdad es la base de datos.
/// </summary>
public sealed record ParametrosNegocio
{
    public DateTime FechaBase { get; init; } = new(2026, 1, 1);

    public int OcDiasAlta { get; init; } = 90;
    public int OcDiasMedia { get; init; } = 60;
    public int OcDiasBaja { get; init; } = 30;
    public int OcDiasSinAsignar { get; init; } = 60;
    public int OcDiasVencePronto { get; init; } = 7;
    public int OcDiasMargenVentana { get; init; } = 10;

    public int TicketDiasVencePronto { get; init; } = 2;
    public int TicketPctRiesgoSla { get; init; } = 80;

    /// <summary>Umbral "urgente" de las alertas de SLA: faltando estos minutos o menos.</summary>
    public int AlertasMinutosAntelacion { get; init; } = 60;
    /// <summary>Desde cuántos minutos antes del vencimiento empieza a avisarse (aviso temprano).</summary>
    public int AlertasMinutosAvisoTemprano { get; init; } = 240;
    public int SeguimientoDiasSinAvance { get; init; } = 7;
    public int CacheCatalogosMinutos { get; init; } = 30;
    public int ConsultasUmbralLentaMs { get; init; } = 1000;

    /// <summary>Días hasta el vencimiento de una OC según su complejidad (null = sin registro).</summary>
    public int DiasOc(NivelComplejidad? nivel) => nivel switch
    {
        NivelComplejidad.Alta => OcDiasAlta,
        NivelComplejidad.Media => OcDiasMedia,
        NivelComplejidad.Baja => OcDiasBaja,
        _ => OcDiasSinAsignar
    };

    /// <summary>Mayor plazo posible de una OC: define cuánto mirar hacia atrás para no perder ninguna.</summary>
    public int OcDiasMaximo => Math.Max(Math.Max(OcDiasAlta, OcDiasMedia), Math.Max(OcDiasBaja, OcDiasSinAsignar));

    /// <summary>Ventana hacia atrás (días) para buscar OC que pueden vencer en el período pedido.</summary>
    public int OcDiasVentana => OcDiasMaximo + OcDiasMargenVentana;
}

/// <summary>Miembro del grupo de analistas ya resuelto contra ca_contact.</summary>
public sealed record MiembroGrupo(string UserId, string Nombre, byte[] Uuid)
{
    public string UuidHex => "0x" + Convert.ToHexString(Uuid);
}
