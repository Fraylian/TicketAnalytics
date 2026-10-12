namespace TicketAnalytics.Domain.Enums;

/// <summary>
/// Etapa operativa real de un ticket dentro del flujo de trabajo del area,
/// independiente del estado que tenga en CA SDM (mdb). Un ticket puede estar
/// "cerrado" en mdb pero seguir "En QA" o "Pendiente de pase" aqui.
/// </summary>
public enum EtapaSeguimiento
{
    PendienteAnalisis = 0,
    EnAnalisis = 1,
    EnDesarrollo = 2,
    EnEsperaProveedor = 3,
    ReportadoProveedor = 4,
    EnEsperaUsuario = 5,
    EnPruebasInternas = 6,
    EnQA = 7,
    PendientePase = 8,
    PendienteOrdenCambio = 9,
    EnImplementacion = 10,
    EnProduccion = 11,
    Cerrado = 12,
    Cancelado = 13,
    Reabierto = 14
}
