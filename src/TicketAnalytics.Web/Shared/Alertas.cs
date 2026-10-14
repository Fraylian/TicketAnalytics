using TicketAnalytics.Web.Models;

namespace TicketAnalytics.Web.Shared;

/// <summary>Ticket abierto del grupo cuyo SLA vence dentro de la ventana de aviso.</summary>
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
            return $"vence en {Formato.Duracion(m)}";
        }
    }

    /// <summary>Nivel de la alerta: critico (≤15 min), alto (≤ umbral urgente) o medio (aviso temprano).</summary>
    public string Nivel => ReglaAlertas.NivelDe(MinutosRestantes);

    /// <summary>Cuenta regresiva compacta para el reloj del aviso: "45" min o "2:15" h.</summary>
    public (string Valor, string Unidad) Reloj
    {
        get
        {
            var m = Math.Max(0, (int)Math.Ceiling(MinutosRestantes));
            return m < 60 ? (m.ToString(), "min") : ($"{m / 60}:{m % 60:00}", "h");
        }
    }
}

/// <summary>
/// Regla de negocio de las alertas de vencimiento (una sola fuente para toda la app).
/// Escalonada: se avisa desde <see cref="MinutosAvisoTemprano"/> antes (aviso), la alerta pasa a
/// urgente faltando <see cref="MinutosAntelacion"/> o menos y a crítica con 15 min o menos.
/// Ambos valores salen de los parámetros de negocio (Alertas.MinutosAvisoTemprano y
/// Alertas.MinutosAntelacion) cuando AlertasVencimiento los carga.
/// </summary>
public static class ReglaAlertas
{
    /// <summary>Umbral urgente (parámetro Alertas.MinutosAntelacion).</summary>
    public static int MinutosAntelacion { get; set; } = 60;

    /// <summary>Desde cuándo se avisa (parámetro Alertas.MinutosAvisoTemprano). Nunca menor al urgente.</summary>
    public static int MinutosAvisoTemprano
    {
        get => Math.Max(_avisoTemprano, MinutosAntelacion);
        set => _avisoTemprano = value;
    }
    private static int _avisoTemprano = 240;

    public const int MinutosCritico = 15;

    /// <summary>Cada cuánto se vuelve a consultar la bandeja mientras la app está abierta.</summary>
    public static readonly TimeSpan IntervaloConsulta = TimeSpan.FromMinutes(2);

    public static string NivelDe(double minutosRestantes)
        => minutosRestantes <= MinutosCritico ? "critico" : minutosRestantes <= MinutosAntelacion ? "alto" : "medio";

    /// <summary>Orden de gravedad del nivel (para avisar de nuevo cuando un ticket sube de nivel).</summary>
    public static int Gravedad(string nivel) => nivel switch { "critico" => 3, "alto" => 2, _ => 1 };

    /// <summary>Tickets de la bandeja que vencen dentro de la ventana de aviso temprano.</summary>
    public static List<AlertaTicket> Proximos(BandejaAnalistasDto bandeja)
    {
        var ahora = DateTime.Now;
        var limite = ahora.AddMinutes(MinutosAvisoTemprano);
        return bandeja.Analistas
            .SelectMany(a => a.Tickets.Select(t => (a.Analista, t)))
            .Where(x => x.t.FechaVencimiento.HasValue && x.t.FechaVencimiento.Value > ahora && x.t.FechaVencimiento.Value <= limite)
            .Select(x => new AlertaTicket(x.t.NoTicket, x.t.Titulo, x.Analista, x.t.FechaVencimiento!.Value))
            .OrderBy(a => a.FechaVencimiento)
            .ToList();
    }

    /// <summary>Vence dentro del umbral urgente (por defecto, la próxima hora).</summary>
    public static bool VenceEnLaProximaHora(DateTime? vencimiento)
        => vencimiento.HasValue && vencimiento.Value > DateTime.Now && vencimiento.Value <= DateTime.Now.AddMinutes(MinutosAntelacion);

    /// <summary>Vence dentro de la ventana de aviso temprano (pero todavía no es urgente).</summary>
    public static bool VenceEnAvisoTemprano(DateTime? vencimiento)
        => vencimiento.HasValue && vencimiento.Value > DateTime.Now.AddMinutes(MinutosAntelacion)
           && vencimiento.Value <= DateTime.Now.AddMinutes(MinutosAvisoTemprano);
}
