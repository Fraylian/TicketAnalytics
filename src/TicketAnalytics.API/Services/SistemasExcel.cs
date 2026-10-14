using ClosedXML.Excel;
using TicketAnalytics.Application.Dashboard.DTOs;

namespace TicketAnalytics.API.Services;

/// <summary>
/// Excel real (.xlsx) del bloque "Sistemas afectados" del dashboard:
/// hoja 1 = matriz sistema × tipo de solución (con proveedor y total);
/// hoja 2 = los casos clasificados del período, uno por fila.
/// </summary>
public static class SistemasExcel
{
    private static readonly XLColor Azul = XLColor.FromHtml("#15528E");
    private static readonly XLColor AzulSuave = XLColor.FromHtml("#EAF2F9");
    private static readonly XLColor Borde = XLColor.FromHtml("#D2DBE6");

    public static byte[] Generar(IndicadoresGestionDto ind, IReadOnlyList<CasoClasificadoDto> casos, string contexto)
    {
        using var wb = new XLWorkbook();
        HojaMatriz(wb, ind, contexto);
        HojaCasos(wb, casos, contexto);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void HojaMatriz(XLWorkbook wb, IndicadoresGestionDto ind, string contexto)
    {
        var ws = Hoja(wb, "Por sistema", "CASOS POR SISTEMA Y TIPO DE SOLUCIÓN", contexto, ind.SistemasConSolucion.Sum(s => s.Total));

        // Columnas de solución: solo las que aparecen en el período, en orden de frecuencia.
        var soluciones = ind.SistemasConSolucion.SelectMany(s => s.Soluciones)
            .GroupBy(x => x.Nombre).OrderByDescending(g => g.Sum(x => x.Cantidad)).Select(g => g.Key).ToList();

        var cab = new List<string> { "Sistema", "Proveedor", "Total casos" };
        cab.AddRange(soluciones);
        cab.Add("Sin solución registrada");
        Encabezado(ws, cab);

        var fila = 5;
        foreach (var s in ind.SistemasConSolucion)
        {
            var c = 1;
            ws.Cell(fila, c++).Value = s.Sistema;
            ws.Cell(fila, c++).Value = s.Proveedor ?? "Por asignar";
            ws.Cell(fila, c++).Value = s.Total;
            foreach (var sol in soluciones)
            {
                var n = s.Soluciones.FirstOrDefault(x => x.Nombre == sol)?.Cantidad ?? 0;
                if (n > 0) ws.Cell(fila, c).Value = n;
                c++;
            }
            if (s.SinSolucion > 0) ws.Cell(fila, c).Value = s.SinSolucion;
            fila++;
        }

        var ultimaDatos = fila - 1;

        // Fila de totales con fórmula: sigue cuadrando si el usuario filtra en Excel.
        if (ind.SistemasConSolucion.Count > 0)
        {
            ws.Cell(fila, 1).Value = "TOTAL";
            for (var col = 3; col <= cab.Count; col++)
            {
                var letra = XLHelper.GetColumnLetterFromNumber(col);
                ws.Cell(fila, col).FormulaA1 = $"SUBTOTAL(9,{letra}5:{letra}{fila - 1})";
            }
            var tot = ws.Range(fila, 1, fila, cab.Count);
            tot.Style.Font.Bold = true;
            tot.Style.Fill.BackgroundColor = AzulSuave;
            fila++;
        }

        if (ind.SinSistema > 0)
        {
            ws.Cell(fila + 1, 1).Value = $"{ind.SinSistema} caso(s) gestionados aún no tienen sistema asignado.";
            ws.Cell(fila + 1, 1).Style.Font.Italic = true;
        }

        Cerrar(ws, cab.Count, ultimaDatos);
    }

    private static void HojaCasos(XLWorkbook wb, IReadOnlyList<CasoClasificadoDto> casos, string contexto)
    {
        var ws = Hoja(wb, "Casos", "CASOS CLASIFICADOS", contexto, casos.Count);
        string[] cab = { "Caso", "Tipo", "Sistema", "Proveedor", "Tipo de solución", "Tipo de actualización",
                         "Tipo de corrección", "Etapa", "Técnico", "Reportado a proveedor", "Causa raíz", "Registrado" };
        Encabezado(ws, cab);

        var fila = 5;
        foreach (var x in casos)
        {
            var c = 1;
            ws.Cell(fila, c++).Value = x.RefNum;
            ws.Cell(fila, c++).Value = x.TipoOrigen == "chg" ? "Orden de cambio" : "Ticket";
            ws.Cell(fila, c++).Value = x.Sistema ?? "Sin sistema";
            ws.Cell(fila, c++).Value = x.Proveedor ?? (x.Sistema is null ? "" : "Por asignar");
            ws.Cell(fila, c++).Value = x.TipoSolucion;
            ws.Cell(fila, c++).Value = x.TipoActualizacion;
            ws.Cell(fila, c++).Value = x.TipoCorreccion;
            ws.Cell(fila, c++).Value = x.Etapa;
            ws.Cell(fila, c++).Value = x.Tecnico;
            ws.Cell(fila, c++).Value = x.ReportadoProveedor;
            ws.Cell(fila, c++).Value = x.CausaRaiz is { Length: > 32000 } cr ? cr[..32000] + "…" : x.CausaRaiz;
            ws.Cell(fila, c).Value = x.CreadoEn.ToLocalTime();
            ws.Cell(fila, c).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
            fila++;
        }
        Cerrar(ws, cab.Length, fila - 1);
        ws.Column(11).Width = 50;
        ws.Column(11).Style.Alignment.WrapText = true;
    }

    // ================= formato común (mismo estilo que ListadosExcel) =================

    private static IXLWorksheet Hoja(XLWorkbook wb, string nombre, string titulo, string contexto, int total)
    {
        var ws = wb.Worksheets.Add(nombre);
        ws.Style.Font.FontName = "Calibri";
        ws.Style.Font.FontSize = 10;
        ws.Cell(1, 1).Value = $"BANCO SANTA CRUZ · {titulo}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(1, 1).Style.Font.FontColor = Azul;
        ws.Cell(2, 1).Value = $"{contexto} · {total} casos · generado {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#5A6B7B");
        ws.Cell(2, 1).Style.Font.Italic = true;
        return ws;
    }

    private static void Encabezado(IXLWorksheet ws, IReadOnlyList<string> cab)
    {
        for (var i = 0; i < cab.Count; i++)
        {
            var c = ws.Cell(4, i + 1);
            c.Value = cab[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Fill.BackgroundColor = Azul;
            c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            c.Style.Alignment.WrapText = true;
        }
        ws.Row(4).Height = 30;
    }

    private static void Cerrar(IXLWorksheet ws, int columnas, int ultimaFila)
    {
        var ultima = Math.Max(4, ultimaFila);
        var rango = ws.Range(4, 1, ultima, columnas);
        rango.SetAutoFilter();
        rango.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        rango.Style.Border.InsideBorderColor = Borde;
        rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        rango.Style.Border.OutsideBorderColor = Borde;
        ws.SheetView.FreezeRows(4);
        ws.Columns(1, columnas).AdjustToContents(4, ultima);
        foreach (var col in ws.Columns(1, columnas))
            col.Width = Math.Clamp(col.Width + 2, 10, 40);
    }
}
