using TicketAnalytics.Application.Tickets.DTOs;

namespace TicketAnalytics.API.Services;

/// <summary>Texto corto que describe el filtro aplicado, para la segunda línea de cada Excel.</summary>
public static class ContextoExport
{
    public static string Rango(DateTime? desde, DateTime? hasta)
    {
        if (desde.HasValue && hasta.HasValue) return $"Período {desde:dd/MM/yyyy} – {hasta:dd/MM/yyyy}";
        if (desde.HasValue) return $"Desde {desde:dd/MM/yyyy}";
        if (hasta.HasValue) return $"Hasta {hasta:dd/MM/yyyy}";
        return "Todo el histórico disponible";
    }

    public static string Oc(OrdenCambioFiltro f)
    {
        var partes = new List<string>();
        if (f.MesVencimiento.HasValue && f.AnioVencimiento.HasValue)
            partes.Add($"Vencen en {f.MesVencimiento:00}/{f.AnioVencimiento}");
        else if (f.AnioVencimiento.HasValue)
            partes.Add($"Vencimiento {f.AnioVencimiento}");
        if (!string.IsNullOrWhiteSpace(f.AsignatarioNombre)) partes.Add($"Analista {f.AsignatarioNombre}");
        if (f.Vencidas == true) partes.Add("Solo vencidas");
        if (f.VencenEnDias.HasValue) partes.Add($"Vencen en {f.VencenEnDias} días");
        if (f.SinAnalista == true) partes.Add("Sin analista");
        if (f.SoloAbiertas == true) partes.Add("Solo abiertas");
        if (!string.IsNullOrWhiteSpace(f.Texto)) partes.Add($"Búsqueda \"{f.Texto}\"");
        return partes.Count == 0 ? "Todas las órdenes del alcance" : string.Join(" · ", partes);
    }
}
