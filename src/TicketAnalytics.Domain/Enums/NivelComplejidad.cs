namespace TicketAnalytics.Domain.Enums;

/// <summary>Complejidad de OC. No existe en CA SDM (risk/impact vacios en 99.9%);
/// TicketAnalytics la gestiona como dato propio en la base local.</summary>
public enum NivelComplejidad { SinAsignar = 0, Baja = 1, Media = 2, Alta = 3 }
