using TicketAnalytics.Web.Models;

namespace TicketAnalytics.Web.Shared;

/// <summary>Ticket abierto del grupo cuyo SLA vence dentro de la ventana de alerta.</summary>
public sealed record AlertaTicket(string NoTicket, string? Titulo, string Analista, DateTime FechaVencimiento)
{
    public double MinutosRestantes => (FechaVencimiento - DateTime.Now).TotalMinutes;

    public string TextoRestante
    {
        get
        {
            var m = MinutosRestantes;
            if (m <= 0) return "venció";
            if (m < 1) return "vence en menos de 1 min";
            if (m < 60) return $"vence en {(int)Math.Ceiling(m)} min";
            var h = (int)(m / 60);
            var resto = (int)Math.Round(m % 60);
            return resto == 0 ? $"vence en {h} h" : $"vence en {h} h {resto} min";
        }
    }

    /// <summary>Clase visual según urgencia: menos de 15 min es crítico.</summary>
    public string Nivel => MinutosRestantes <= 15 ? "critico" : MinutosRestantes <= 30 ? "alto" : "medio";
}

/// <summary>Regla de negocio de las alertas de vencimiento (una sola fuente para toda la app).</summary>
public static class ReglaAlertas
{
    /// <summary>Antelación con la que se avisa: el requerimiento pide al menos 1 hora.</summary>
    public const int MinutosAntelacion = 60;

    /// <summary>Cada cuánto se vuelve a consultar la bandeja mientras la app está abierta.</summary>
    public static readonly TimeSpan IntervaloConsulta = TimeSpan.FromMinutes(2);

    /// <summary>Tickets de la bandeja que vencen en los próximos <see cref="MinutosAntelacion"/> minutos.</summary>
    public static List<AlertaTicket> Proximos(BandejaAnalistasDto bandeja)
    {
        var ahora = DateTime.Now;
        var limite = ahora.AddMinutes(MinutosAntelacion);
        return bandeja.Analistas
            .SelectMany(a => a.Tickets.Select(t => (a.Analista, t)))
            .Where(x => x.t.FechaVencimiento.HasValue && x.t.FechaVencimiento.Value > ahora && x.t.FechaVencimiento.Value <= limite)
            .Select(x => new AlertaTicket(x.t.NoTicket, x.t.Titulo, x.Analista, x.t.FechaVencimiento!.Value))
            .OrderBy(a => a.FechaVencimiento)
            .ToList();
    }

    public static bool VenceEnLaProximaHora(DateTime? vencimiento)
        => vencimiento.HasValue && vencimiento.Value > DateTime.Now && vencimiento.Value <= DateTime.Now.AddMinutes(MinutosAntelacion);
}
