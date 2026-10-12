using ClosedXML.Excel;
using TicketAnalytics.Application.Tickets.DTOs;
using TicketAnalytics.Application.TrabajoDiario.DTOs;

namespace TicketAnalytics.API.Services;

/// <summary>
/// Excel real (.xlsx) de los listados operativos: Tickets, Órdenes de cambio y Diario TI.
/// Reemplaza las antiguas exportaciones CSV del navegador, que además salían de una sola
/// página de resultados. Aquí siempre se exporta TODO lo que cumple el filtro.
/// Formato común: título con branding, línea de contexto, tabla con autofiltro,
/// encabezado fijo, anchos ajustados y fila de totales cuando aplica.
/// </summary>
public static class ListadosExcel
{
    private static readonly XLColor Azul = XLColor.FromHtml("#15528E");
    private static readonly XLColor AzulSuave = XLColor.FromHtml("#EAF2F9");
    private static readonly XLColor Rojo = XLColor.FromHtml("#C62828");
    private static readonly XLColor RojoSuave = XLColor.FromHtml("#FDECEA");
    private static readonly XLColor Verde = XLColor.FromHtml("#2E7D32");
    private static readonly XLColor Ambar = XLColor.FromHtml("#B45309");

    public static byte[] Tickets(IReadOnlyList<TicketEnriquecidoDto> tickets, string contexto)
    {
        using var wb = new XLWorkbook();
        var ws = Hoja(wb, "Tickets", "LISTADO DE TICKETS", contexto, tickets.Count);

        string[] cab =
        {
            "Tipo", "No. Ticket", "Descripción", "Categoría", "Estado", "Usuario final", "Asignatario", "Grupo",
            "Apertura", "Cierre solicitado", "Vencimiento SLA", "Días para vencer", "Dentro SLA",
            "No. OC", "Estado OC", "Asignatario OC", "Apertura OC", "Cierre OC", "Complejidad OC", "OC dentro SLA",
            "Cerrado con OC pendiente"
        };
        var fila = Encabezado(ws, cab);

        foreach (var t in tickets)
        {
            var c = 1;
            ws.Cell(fila, c++).Value = t.TipoCaso;
            ws.Cell(fila, c++).Value = t.NoTicket;
            ws.Cell(fila, c++).Value = Recortar(t.Descripcion);
            ws.Cell(fila, c++).Value = t.Categoria;
            ws.Cell(fila, c++).Value = t.Estado;
            ws.Cell(fila, c++).Value = t.UsuarioFinalAfectado;
            ws.Cell(fila, c++).Value = t.Asignatario;
            ws.Cell(fila, c++).Value = t.Grupo;
            Fecha(ws.Cell(fila, c++), t.FechaApertura, true);
            Fecha(ws.Cell(fila, c++), t.FechaCierreSolicitado, true);
            Fecha(ws.Cell(fila, c++), t.FechaVencimiento, true);
            if (t.DiasParaVencer.HasValue) ws.Cell(fila, c).Value = t.DiasParaVencer.Value;
            c++;
            var sla = ws.Cell(fila, c++);
            sla.Value = t.ResueltoDentroSLA;
            ColorSiNo(sla, t.ResueltoDentroSLA);
            ws.Cell(fila, c++).Value = t.NoOC;
            ws.Cell(fila, c++).Value = t.EstadoOC;
            ws.Cell(fila, c++).Value = t.AsignatarioOC;
            Fecha(ws.Cell(fila, c++), t.FechaAperturaOC, false);
            Fecha(ws.Cell(fila, c++), t.FechaCierreSolicitadoOC, false);
            ws.Cell(fila, c++).Value = t.ComplejidadOC;
            ws.Cell(fila, c++).Value = t.OCResueltoDentroSLA;
            ws.Cell(fila, c++).Value = t.ReqCerradoConCambioPendiente ? "SI" : "NO";
            if (t.ReqCerradoConCambioPendiente) ws.Range(fila, 1, fila, cab.Length).Style.Fill.BackgroundColor = RojoSuave;
            fila++;
        }

        Cerrar(ws, cab.Length, fila, anchoDescripcion: 3);
        return Guardar(wb);
    }

    public static byte[] OrdenesCambio(IReadOnlyList<OrdenCambioConTicketsDto> ordenes, string contexto)
    {
        using var wb = new XLWorkbook();
        var ws = Hoja(wb, "Órdenes de cambio", "ÓRDENES DE CAMBIO", contexto, ordenes.Count);

        string[] cab =
        {
            "No. OC", "Resumen", "Estado", "Analista", "Categoría", "Complejidad", "Apertura", "Vencimiento",
            "Días restantes", "% tiempo consumido", "Situación", "Cierre", "Tickets", "Ticket cerrado con OC pendiente",
            "Última gestión", "Días sin gestión"
        };
        var fila = Encabezado(ws, cab);

        foreach (var o in ordenes)
        {
            var c = 1;
            ws.Cell(fila, c++).Value = o.NoOC;
            ws.Cell(fila, c++).Value = Recortar(o.ResumenOC);
            ws.Cell(fila, c++).Value = o.EstadoOC;
            ws.Cell(fila, c++).Value = o.AsignatarioOC ?? "Sin asignar";
            ws.Cell(fila, c++).Value = o.CategoriaOC;
            ws.Cell(fila, c++).Value = o.ComplejidadOC ?? "Sin asignar";
            Fecha(ws.Cell(fila, c++), o.FechaAperturaOC, false);
            Fecha(ws.Cell(fila, c++), o.FechaVencimientoOC, false);
            if (o.DiasRestantes.HasValue) ws.Cell(fila, c).Value = o.DiasRestantes.Value;
            c++;
            if (o.PctTiempoConsumido.HasValue)
            {
                ws.Cell(fila, c).Value = o.PctTiempoConsumido.Value / 100.0;
                ws.Cell(fila, c).Style.NumberFormat.Format = "0%";
            }
            c++;
            var sit = ws.Cell(fila, c++);
            sit.Value = TextoSituacion(o.EstadoVencimiento);
            sit.Style.Font.Bold = true;
            sit.Style.Font.FontColor = o.EstadoVencimiento switch
            {
                "vencida" or "vence_hoy" => Rojo,
                "vence_pronto" => Ambar,
                "completada" => Verde,
                _ => XLColor.FromHtml("#1A2733")
            };
            Fecha(ws.Cell(fila, c++), o.FechaCierreOC, false);
            ws.Cell(fila, c++).Value = o.CantidadTickets;
            ws.Cell(fila, c++).Value = o.TieneTicketCerradoOcPendiente ? "SI" : "NO";
            Fecha(ws.Cell(fila, c++), o.UltimaGestion, false);
            if (o.DiasDesdeUltimaGestion.HasValue) ws.Cell(fila, c).Value = o.DiasDesdeUltimaGestion.Value;
            if (o.EstadoVencimiento == "vencida") ws.Range(fila, 1, fila, cab.Length).Style.Fill.BackgroundColor = RojoSuave;
            fila++;
        }

        Cerrar(ws, cab.Length, fila, anchoDescripcion: 2);
        return Guardar(wb);
    }

    public static byte[] Actividades(IReadOnlyList<ActividadDto> actividades, string contexto)
    {
        using var wb = new XLWorkbook();
        var ws = Hoja(wb, "Diario TI", "DIARIO DE ACTIVIDADES TI", contexto, actividades.Count);

        string[] cab = { "Fecha", "Técnico", "Actividad", "Tipo", "No. Solicitud", "Enlace MDB", "Proyecto", "Horas", "Estado", "Comentario" };
        var fila = Encabezado(ws, cab);
        var primeraFila = fila;

        foreach (var a in actividades)
        {
            var c = 1;
            ws.Cell(fila, c).Value = a.Fecha.ToDateTime(TimeOnly.MinValue);
            ws.Cell(fila, c++).Style.DateFormat.Format = "dd/mm/yyyy";
            ws.Cell(fila, c++).Value = a.TecnicoNombre;
            ws.Cell(fila, c++).Value = Recortar(a.Actividad);
            ws.Cell(fila, c++).Value = a.TipoTexto;
            ws.Cell(fila, c++).Value = a.NoSolicitud;
            ws.Cell(fila, c++).Value = a.RefNumVinculado;
            ws.Cell(fila, c++).Value = a.Proyecto;
            ws.Cell(fila, c).Value = a.Horas;
            ws.Cell(fila, c++).Style.NumberFormat.Format = "0.00";
            ws.Cell(fila, c++).Value = a.EstadoTexto;
            ws.Cell(fila, c++).Value = Recortar(a.Comentario);
            fila++;
        }

        // Totales con fórmula: si el usuario filtra o edita en Excel, el total sigue siendo correcto.
        ws.Cell(fila, 7).Value = "TOTAL HORAS";
        ws.Cell(fila, 7).Style.Font.Bold = true;
        ws.Cell(fila, 8).FormulaA1 = actividades.Count > 0 ? $"SUBTOTAL(9,H{primeraFila}:H{fila - 1})" : "0";
        ws.Cell(fila, 8).Style.Font.Bold = true;
        ws.Cell(fila, 8).Style.NumberFormat.Format = "0.00";
        ws.Range(fila, 1, fila, cab.Length).Style.Fill.BackgroundColor = AzulSuave;

        Cerrar(ws, cab.Length, fila, anchoDescripcion: 3);
        return Guardar(wb);
    }

    // ================= utilidades de formato =================

    private static IXLWorksheet Hoja(XLWorkbook wb, string nombre, string titulo, string contexto, int total)
    {
        var ws = wb.Worksheets.Add(nombre);
        ws.Style.Font.FontName = "Calibri";
        ws.Style.Font.FontSize = 10;

        ws.Cell(1, 1).Value = $"BANCO SANTA CRUZ · {titulo}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(1, 1).Style.Font.FontColor = Azul;

        ws.Cell(2, 1).Value = $"{contexto} · {total} registros · generado {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#5A6B7B");
        ws.Cell(2, 1).Style.Font.Italic = true;
        return ws;
    }

    /// <summary>Escribe el encabezado en la fila 4 y devuelve la primera fila de datos.</summary>
    private static int Encabezado(IXLWorksheet ws, string[] cab)
    {
        const int f = 4;
        for (var i = 0; i < cab.Length; i++)
        {
            var c = ws.Cell(f, i + 1);
            c.Value = cab[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Fill.BackgroundColor = Azul;
            c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            c.Style.Alignment.WrapText = true;
        }
        ws.Row(f).Height = 30;
        return f + 1;
    }

    private static void Cerrar(IXLWorksheet ws, int columnas, int siguienteFila, int anchoDescripcion)
    {
        var ultimaDatos = Math.Max(4, siguienteFila - 1);
        var rango = ws.Range(4, 1, ultimaDatos, columnas);
        rango.SetAutoFilter();
        rango.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        rango.Style.Border.InsideBorderColor = XLColor.FromHtml("#D2DBE6");
        rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        rango.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D2DBE6");
        ws.SheetView.FreezeRows(4);

        ws.Columns(1, columnas).AdjustToContents(4, ultimaDatos);
        foreach (var col in ws.Columns(1, columnas))
            col.Width = Math.Clamp(col.Width + 2, 10, 45);
        ws.Column(anchoDescripcion).Width = 60;
        ws.Column(anchoDescripcion).Style.Alignment.WrapText = true;
    }

    private static void Fecha(IXLCell c, DateTime? v, bool conHora)
    {
        if (!v.HasValue) return;
        c.Value = v.Value;
        c.Style.DateFormat.Format = conHora ? "dd/mm/yyyy hh:mm" : "dd/mm/yyyy";
    }

    private static void ColorSiNo(IXLCell c, string? v)
    {
        if (v == "SI") c.Style.Font.FontColor = Verde;
        else if (v == "NO") { c.Style.Font.FontColor = Rojo; c.Style.Font.Bold = true; }
    }

    private static string TextoSituacion(string estado) => estado switch
    {
        "vencida" => "Vencida",
        "vence_hoy" => "Vence hoy",
        "vence_pronto" => "Vence pronto",
        "en_curso" => "En curso",
        "completada" => "Completada",
        _ => "Sin fecha"
    };

    /// <summary>Excel admite 32.767 caracteres por celda; recortamos con margen.</summary>
    private static string? Recortar(string? s) => s is { Length: > 32000 } ? s[..32000] + "…" : s;

    private static byte[] Guardar(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
