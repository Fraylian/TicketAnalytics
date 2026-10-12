using System.Net;

namespace TicketAnalytics.Web.Services;

/// <summary>
/// Error de llamada a la API con dos niveles de mensaje: uno legible para el
/// usuario (MensajeUsuario) y el detalle técnico (DetalleTecnico) para log/soporte.
/// Evita mostrar stack traces o texto crudo del servidor en un Snackbar.
/// </summary>
public sealed class ApiException : Exception
{
    public string MensajeUsuario { get; }
    public string? DetalleTecnico { get; }
    public HttpStatusCode CodigoEstado { get; }

    public ApiException(string mensajeUsuario, string? detalleTecnico, HttpStatusCode codigo)
        : base(mensajeUsuario)
    {
        MensajeUsuario = mensajeUsuario;
        DetalleTecnico = Recortar(detalleTecnico);
        CodigoEstado = codigo;
    }

    /// <summary>El detalle técnico recortado: nunca volcamos un stack completo a la UI.</summary>
    private static string? Recortar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length > 300 ? s[..300] + "…" : s;
    }
}

/// <summary>Forma mínima de ProblemDetails (RFC 7807) que devuelve la API.</summary>
public sealed class ProblemDetailsDto
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int? Status { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
}
