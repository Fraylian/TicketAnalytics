using System.Text;
using Microsoft.JSInterop;
using TicketAnalytics.Web.Models;

namespace TicketAnalytics.Web.Services;

/// <summary>Exporta a CSV y dispara la descarga en el navegador via JS interop.</summary>
public static class CsvExport
{
    private static IJSRuntime? _js;
    public static void Init(IJSRuntime js) => _js = js;

    public static async Task DescargarTicketsAsync(IEnumerable<TicketEnriquecidoDto> tickets)
    {
        if (_js is null) return;
        var sb = new StringBuilder();
        sb.AppendLine("Tipo;No_Ticket;Categoria;Estado;Usuario_Final;Asignatario;Fecha_Apertura;Fecha_Cierre;SLA;No_OC;Estado_OC;Asignatario_OC;Apertura_OC;Cierre_OC;Complejidad_OC;OC_SLA");
        foreach (var t in tickets)
        {
            sb.AppendLine(string.Join(';',
                Q(t.TipoCaso), Q(t.NoTicket), Q(t.Categoria), Q(t.Estado), Q(t.UsuarioFinalAfectado), Q(t.Asignatario),
                Q(t.FechaApertura?.ToString("dd/MM/yyyy")), Q(t.FechaCierreSolicitado?.ToString("dd/MM/yyyy")), Q(t.ResueltoDentroSLA),
                Q(t.NoOC), Q(t.EstadoOC), Q(t.AsignatarioOC), Q(t.FechaAperturaOC?.ToString("dd/MM/yyyy")),
                Q(t.FechaCierreSolicitadoOC?.ToString("dd/MM/yyyy")), Q(t.ComplejidadOC), Q(t.OCResueltoDentroSLA)));
        }
        var nombre = $"tickets_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        await _js.InvokeVoidAsync("descargarArchivo", nombre, "text/csv;charset=utf-8", sb.ToString());
    }

    public static async Task DescargarActividadesAsync(IEnumerable<ActividadDto> actividades)
    {
        if (_js is null) return;
        var lista = actividades.ToList();
        var sb = new StringBuilder();
        sb.AppendLine("Fecha;Tecnico;Actividad;Tipo;No_Solicitud;Enlace_MDB;Proyecto;Horas;Estado;Comentario");
        foreach (var a in lista)
        {
            sb.AppendLine(string.Join(';',
                Q(a.Fecha.ToString("dd/MM/yyyy")), Q(a.TecnicoNombre), Q(a.Actividad), Q(a.TipoTexto),
                Q(a.NoSolicitud), Q(a.RefNumVinculado), Q(a.Proyecto), Q(a.Horas.ToString("0.##")),
                Q(a.EstadoTexto), Q(a.Comentario)));
        }
        // Fila de totales
        sb.AppendLine(string.Join(';', "", "", "", "", "", "", "TOTAL", Q(lista.Sum(x => x.Horas).ToString("0.##")), "", ""));

        var nombre = $"trabajo_diario_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        await _js.InvokeVoidAsync("descargarArchivo", nombre, "text/csv;charset=utf-8", sb.ToString());
    }

    public static async Task DescargarOrdenesCambioAsync(IEnumerable<OrdenCambioConTicketsDto> ordenes)
    {
        if (_js is null) return;
        var lista = ordenes.ToList();
        var sb = new StringBuilder();
        sb.AppendLine("No_OC;Resumen;Estado;Analista;Categoria;Complejidad;Apertura;Vencimiento;Dias_Restantes;Estado_Vencimiento;Cierre;Tickets;Ticket_Cerrado_OC_Pendiente;Ultima_Gestion");
        foreach (var o in lista)
        {
            sb.AppendLine(string.Join(';',
                Q(o.NoOC), Q(o.ResumenOC), Q(o.EstadoOC), Q(o.AsignatarioOC), Q(o.CategoriaOC), Q(o.ComplejidadOC),
                Q(o.FechaAperturaOC?.ToString("dd/MM/yyyy")), Q(o.FechaVencimientoOC?.ToString("dd/MM/yyyy")),
                Q(o.DiasRestantes?.ToString()), Q(o.EstadoVencimiento), Q(o.FechaCierreOC?.ToString("dd/MM/yyyy")),
                Q(o.CantidadTickets.ToString()), Q(o.TieneTicketCerradoOcPendiente ? "SI" : "NO"),
                Q(o.UltimaGestion?.ToString("dd/MM/yyyy"))));
        }
        var nombre = $"ordenes_cambio_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        await _js.InvokeVoidAsync("descargarArchivo", nombre, "text/csv;charset=utf-8", sb.ToString());
    }

    public static async Task DescargarReportesProveedorAsync(IEnumerable<ReporteProveedorMatrizDto> reportes, string nombreBase)
    {
        if (_js is null) return;
        var lista = reportes.ToList();
        var sb = new StringBuilder();
        // 4 columnas principales + contexto útil para la matriz
        sb.AppendLine("Caso_Proveedor;Titulo;Descripcion;Agenda_Solucion;Proveedor;Ticket_Sistema;Responsable;Fecha_Reporte;Urgencia;SLA_Horas;Estado_SLA;Brindo_Asistencia;Entrego_Cambio;Estado");
        foreach (var r in lista)
        {
            sb.AppendLine(string.Join(';',
                Q(r.NumeroCasoExterno), Q(r.Titulo), Q(r.Descripcion), Q(r.ResultadoSolucion),
                Q(r.Proveedor), Q(r.RefNumSistema), Q(r.ResponsableInterno), Q(r.FechaReporte.ToString("dd/MM/yyyy")),
                Q(r.Urgencia), Q(r.SlaReferenciaHoras?.ToString()), Q(r.CumplioSla == true ? "SI" : r.CumplioSla == false ? "NO" : "Sin evaluar"),
                Q(r.BrindoAsistencia == true ? "SI" : r.BrindoAsistencia == false ? "NO" : ""),
                Q(r.EntregoCambio == true ? "SI" : r.EntregoCambio == false ? "NO" : ""), Q(r.EstadoTexto)));
        }
        await _js.InvokeVoidAsync("descargarArchivo", $"{nombreBase}.csv", "text/csv;charset=utf-8", sb.ToString());
    }

    private static string Q(string? v)
    {
        v ??= "";
        if (v.Contains(';') || v.Contains('"') || v.Contains('\n'))
            v = "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }
}
