using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace TicketAnalytics.Web.Shared;

/// <summary>Utilidades de presentación compartidas (antes duplicadas en cada página).</summary>
public static class Formato
{
    public static readonly CultureInfo Cultura = new("es-DO");

    private static readonly string[] ColoresAvatar =
    {
        "#15528E", "#00897B", "#6B4EFF", "#C75100", "#7B1FA2", "#2E7D32", "#AD1457", "#455A64", "#0F3D6B"
    };

    public static string Iniciales(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "?";
        var p = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return p.Length >= 2 ? $"{p[0][0]}{p[^1][0]}".ToUpperInvariant() : nombre[..Math.Min(2, nombre.Length)].ToUpperInvariant();
    }

    /// <summary>Color estable por persona: el mismo analista siempre tiene el mismo color.</summary>
    public static string ColorAvatar(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "#8FA3B8";
        var h = 0;
        foreach (var c in nombre.ToLowerInvariant()) h = (h * 31 + c) & 0x7FFFFFFF;
        return ColoresAvatar[h % ColoresAvatar.Length];
    }

    /// <summary>"Juan Carlos Pérez Gómez" → "Juan Gómez".</summary>
    public static string NombreCorto(string? n)
    {
        if (string.IsNullOrWhiteSpace(n)) return "—";
        var p = n.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return p.Length >= 2 ? $"{p[0]} {p[^1]}" : n;
    }

    /// <summary>
    /// "TI.Problema/Error.Sistemas.Canales &amp; Medios de Pago" → "Incidente · Canales &amp; Medios de Pago".
    /// Se conserva el tipo: Incidente y Solicitud comparten el último segmento y sin él se veían duplicadas.
    /// </summary>
    public static string CategoriaCorta(string? c)
    {
        if (string.IsNullOrWhiteSpace(c)) return "Sin categoría";
        var p = c.Split('.');
        var ultimo = p.Length > 0 ? p[^1].Trim() : c;
        if (c.Contains("Problema/Error", StringComparison.OrdinalIgnoreCase) || c.Contains("Incidente", StringComparison.OrdinalIgnoreCase))
            return $"Incidente · {ultimo}";
        if (c.Contains("Solicitud", StringComparison.OrdinalIgnoreCase))
            return $"Solicitud · {ultimo}";
        return ultimo;
    }

    /// <summary>Solo el área de la categoría ("Canales &amp; Medios de Pago"), cuando el tipo ya se muestra aparte.</summary>
    public static string Area(string? c)
    {
        if (string.IsNullOrWhiteSpace(c)) return "—";
        var p = c.Split('.');
        return p.Length > 0 ? p[^1].Trim() : c;
    }

    /// <summary>Minutos legibles: "45 min", "1 h", "2 h 15 min".</summary>
    public static string Duracion(double minutos)
    {
        var m = (int)Math.Ceiling(Math.Max(0, minutos));
        if (m < 60) return $"{m} min";
        var h = m / 60; var resto = m % 60;
        return resto == 0 ? $"{h} h" : $"{h} h {resto} min";
    }

    public static string NombreMes(int mes) => Capitalizar(Cultura.DateTimeFormat.GetMonthName(mes));

    /// <summary>"2026-10" → "Oct 2026" (eje de los gráficos mensuales).</summary>
    public static string Mes(string? periodo)
    {
        if (string.IsNullOrWhiteSpace(periodo) || periodo.Length < 7
            || !int.TryParse(periodo[..4], out var a) || !int.TryParse(periodo[5..7], out var m) || m is < 1 or > 12)
            return periodo ?? "";
        return $"{Capitalizar(Cultura.DateTimeFormat.GetAbbreviatedMonthName(m).TrimEnd('.'))} {a}";
    }

    public static string Capitalizar(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    public static string Corto(string? t, int max)
        => string.IsNullOrWhiteSpace(t) ? "—" : (t.Length > max ? t[..max].TrimEnd() + "…" : t);

    public static string Porcentaje(double parte, double total) => total <= 0 ? "0%" : $"{Math.Round(parte * 100 / total)}%";

    public static string Horas(double? h)
    {
        if (!h.HasValue) return "—";
        var v = Math.Abs(h.Value);
        if (v >= 48) return $"{h.Value / 24:0.#} d";
        return $"{h.Value:0.#} h";
    }

    public static string Hace(DateTime? t)
    {
        if (!t.HasValue) return "—";
        var d = DateTime.Now - t.Value;
        if (d.TotalMinutes < 1) return "hace un momento";
        if (d.TotalHours < 1) return $"hace {(int)d.TotalMinutes} min";
        if (d.TotalDays < 1) return $"hace {(int)d.TotalHours} h";
        if (d.TotalDays < 30) return $"hace {(int)d.TotalDays} d";
        return t.Value.ToString("dd/MM/yyyy");
    }
}

/// <summary>Lectura del query string para los drill-downs entre pantallas (?vista=…, ?analista=…).</summary>
public static class UrlQuery
{
    public static Dictionary<string, string> Leer(NavigationManager nav)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var query = nav.ToAbsoluteUri(nav.Uri).Query;
        foreach (var par in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = par.IndexOf('=');
            if (i <= 0) continue;
            d[Uri.UnescapeDataString(par[..i])] = Uri.UnescapeDataString(par[(i + 1)..].Replace('+', ' '));
        }
        return d;
    }

    public static string Url(string ruta, params (string Clave, string? Valor)[] pares)
    {
        var q = string.Join("&", pares.Where(p => !string.IsNullOrWhiteSpace(p.Valor))
                                      .Select(p => $"{p.Clave}={Uri.EscapeDataString(p.Valor!)}"));
        return q.Length == 0 ? ruta : $"{ruta}?{q}";
    }
}
