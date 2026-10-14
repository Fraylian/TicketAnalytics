using TicketAnalytics.Domain.Enums;

namespace TicketAnalytics.Domain.Common;

/// <summary>Resultado del calculo de SLA de un ticket, con todos los tiempos.</summary>
public class ResultadoSla
{
    public EstadoSla Estado { get; set; }
    public string EstadoTexto => Estado switch
    {
        EstadoSla.Cumplido => "Dentro de SLA",
        EstadoSla.Incumplido => "SLA vencido",
        EstadoSla.EnCurso => "En curso",
        _ => "N/A"
    };
    public DateTime? FechaApertura { get; set; }
    public DateTime? FechaVencimiento { get; set; }        // fecha prevista de incumplimiento
    public DateTime? FechaCierre { get; set; }
    public double? HorasTranscurridas { get; set; }
    public double? HorasRestantes { get; set; }            // negativo si ya vencio
    public double? PctConsumido { get; set; }              // 0..100+ 
    public bool ProximoAVencer { get; set; }               // < 20% restante y aun abierto
    public bool Vencido { get; set; }
}

/// <summary>
/// Calcula el SLA de un ticket a partir de los epochs de CA SDM y el
/// time_to_violation de attached_sla. Todo en horas.
/// </summary>
public static class CalculoSla
{
    /// <param name="pctRiesgo">% de SLA consumido a partir del cual un ticket abierto está "próximo a vencer"
    /// (parámetro Ticket.PctRiesgoSla de la base local).</param>
    public static ResultadoSla Calcular(int? openDateEpoch, int? closeDateEpoch,
        int? timeToViolationEpoch, int? slaViolStatus, DateTime? ahora = null, int pctRiesgo = 80)
    {
        var r = new ResultadoSla
        {
            FechaApertura = EpochRD.ToDateTime(openDateEpoch),
            FechaVencimiento = EpochRD.ToDateTime(timeToViolationEpoch),
            FechaCierre = EpochRD.ToDateTime(closeDateEpoch)
        };

        // Sin fecha de violacion prevista -> no medible
        if (timeToViolationEpoch is null or 0)
        {
            r.Estado = EstadoSla.NoAplica;
            return r;
        }

        var apertura = r.FechaApertura;
        var vencimiento = r.FechaVencimiento!.Value;
        var referencia = r.FechaCierre ?? (ahora ?? DateTime.Now);

        if (apertura.HasValue)
        {
            var ventanaTotal = (vencimiento - apertura.Value).TotalHours;
            r.HorasTranscurridas = Math.Round((referencia - apertura.Value).TotalHours, 1);
            if (ventanaTotal > 0)
                r.PctConsumido = Math.Round(100.0 * r.HorasTranscurridas.Value / ventanaTotal, 1);
        }

        r.HorasRestantes = Math.Round((vencimiento - referencia).TotalHours, 1);

        // Ticket cerrado: comparamos cierre vs vencimiento
        if (closeDateEpoch is > 0)
        {
            r.Estado = r.FechaCierre <= vencimiento ? EstadoSla.Cumplido : EstadoSla.Incumplido;
            r.Vencido = r.Estado == EstadoSla.Incumplido;
            return r;
        }

        // Ticket abierto
        if (slaViolStatus == 1 || r.HorasRestantes < 0)
        {
            r.Estado = EstadoSla.Incumplido;
            r.Vencido = true;
        }
        else
        {
            r.Estado = EstadoSla.EnCurso;
            r.ProximoAVencer = r.PctConsumido >= pctRiesgo;   // consumido pctRiesgo%+ = próximo a vencer
        }
        return r;
    }
}
