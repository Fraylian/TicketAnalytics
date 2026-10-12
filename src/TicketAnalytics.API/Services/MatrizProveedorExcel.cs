using ClosedXML.Excel;
using TicketAnalytics.Application.Proveedores.DTOs;

namespace TicketAnalytics.API.Services;

/// <summary>
/// Genera el Excel real de la Matriz de Cumplimiento de Proveedores, con dos hojas
/// ("Matriz Control" y "DETALLES") replicando el formato del archivo oficial del banco.
/// </summary>
public static class MatrizProveedorExcel
{
    // Colores del branding BSC
    private static readonly XLColor Azul = XLColor.FromHtml("#15528E");
    private static readonly XLColor Verde = XLColor.FromHtml("#76B82A");
    private static readonly XLColor GrisClaro = XLColor.FromHtml("#EAF2F9");

    public static byte[] Generar(
        string proveedorNombre,
        int mes, int anio,
        string? area,
        string? responsable,
        IReadOnlyList<ReporteProveedorMatrizDto> reportes,
        ProveedorCatalogoDto? proveedor)
    {
        using var wb = new XLWorkbook();
        HojaMatrizControl(wb, proveedorNombre, mes, anio, area, responsable, reportes, proveedor);
        HojaDetalles(wb, reportes);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void HojaMatrizControl(
        XLWorkbook wb, string proveedorNombre, int mes, int anio, string? area, string? responsable,
        IReadOnlyList<ReporteProveedorMatrizDto> reportes, ProveedorCatalogoDto? proveedor)
    {
        var ws = wb.Worksheets.Add("Matriz Control");
        ws.Style.Font.FontName = "Calibri";
        ws.Style.Font.FontSize = 11;

        // Título
        ws.Cell("A1").Value = "MATRIZ CALCULO CUMPLIMIENTO DE PROVEEDORES";
        ws.Range("A1:H1").Merge();
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontColor = XLColor.White;
        ws.Cell("A1").Style.Fill.BackgroundColor = Azul;
        ws.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Row(1).Height = 22;

        var meses = new[] { "", "ENERO", "FEBRERO", "MARZO", "ABRIL", "MAYO", "JUNIO",
                            "JULIO", "AGOSTO", "SEPTIEMBRE", "OCTUBRE", "NOVIEMBRE", "DICIEMBRE" };

        // Encabezado
        Etiqueta(ws, "A2", "FECHA:");           ws.Cell("D2").Value = new DateTime(anio, mes, 1).AddMonths(1).AddDays(-1);
        ws.Cell("D2").Style.DateFormat.Format = "yyyy-mm-dd";
        Etiqueta(ws, "A3", "PERIODO EVALUACION:"); ws.Cell("D3").Value = $"{meses[mes]} {anio}";
        Etiqueta(ws, "A4", "PROVEEDOR:");       ws.Cell("D4").Value = proveedorNombre;
        Etiqueta(ws, "A5", "NOMBRE CONTRATO SISTEMA/SERVICIO:"); ws.Cell("D5").Value = proveedor?.Nombre ?? proveedorNombre;
        Etiqueta(ws, "A6", "AREA SUMINISTRA INFORMACION:"); ws.Cell("D6").Value = area ?? "Desarrollo de Software";
        Etiqueta(ws, "A7", "RESPONSABLE:");     ws.Cell("D7").Value = responsable ?? "";

        // Tabla de severidad
        int f = 9;
        string[] cab = { "SEVERIDAD", "SLA", "", "CANTIDAD SOPORTES", "RESUELTOS DENTRO DEL SLA", "RESULTADOS", "", "CUMPLIMIENTO" };
        for (int i = 0; i < cab.Length; i++)
        {
            var c = ws.Cell(f, i + 1);
            c.Value = cab[i];
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = Azul;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var severidades = new[] { "Critico", "Alto", "Medio", "Bajo" };
        int totalCasos = 0, totalCumplidos = 0;
        for (int i = 0; i < severidades.Length; i++)
        {
            int fila = f + 1 + i;
            var sev = severidades[i];
            var delSev = reportes.Where(r => string.Equals(r.Urgencia, sev, StringComparison.OrdinalIgnoreCase)).ToList();
            int cant = delSev.Count;
            int cumplidos = delSev.Count(r => r.CumplioSla == true);
            totalCasos += cant; totalCumplidos += cumplidos;

            int? slaHoras = proveedor?.SlaPara(sev);
            ws.Cell(fila, 1).Value = sev;
            ws.Cell(fila, 2).Value = slaHoras.HasValue ? $"{slaHoras} Horas" : "";
            // Solo escribimos números cuando hay casos; si no, la celda queda en blanco.
            if (cant > 0)
            {
                ws.Cell(fila, 4).Value = cant;
                ws.Cell(fila, 5).Value = cumplidos;
                ws.Cell(fila, 6).Value = cant == cumplidos ? 1 : 0;
            }
            ws.Range(fila, 1, fila, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Otros acuerdos
        int fo = f + 6;
        ws.Cell(fo, 1).Value = "OTROS ACUERDOS";
        ws.Cell(fo, 4).Value = "SE CUMPLE CON LO ACORDADO";
        ws.Cell(fo, 6).Value = "RESULTADOS";
        ws.Cell(fo, 8).Value = "CUMPLIMIENTO";
        ws.Range(fo, 1, fo, 8).Style.Font.Bold = true;
        ws.Range(fo, 1, fo, 8).Style.Fill.BackgroundColor = GrisClaro;

        string[] acuerdos = { "SLA de soporte vigente", "Contrato vigente", "Soporte de mantenimiento", "Copia actualizada de codigo fuente" };
        for (int i = 0; i < acuerdos.Length; i++)
        {
            int fila = fo + 1 + i;
            ws.Cell(fila, 1).Value = acuerdos[i];
            ws.Cell(fila, 4).Value = "Si";
            ws.Cell(fila, 6).Value = 1;
        }

        // Cumplimiento alcanzado (arriba a la derecha)
        ws.Cell("G2").Value = "CUMPLIMIENTO ALCANZADO";
        ws.Cell("G2").Style.Font.Bold = true;
        double pct = totalCasos > 0 ? (double)totalCumplidos / totalCasos : 1.0;
        ws.Cell("H4").Value = pct;
        ws.Cell("H4").Style.NumberFormat.Format = "0%";
        ws.Cell("H5").Value = pct >= 1.0 ? "En Cumplimiento" : "Incumplimiento";
        ws.Cell("H5").Style.Font.FontColor = pct >= 1.0 ? Verde : XLColor.FromHtml("#C62828");
        ws.Cell("H5").Style.Font.Bold = true;

        ws.Cell(fo + 5, 1).Value = "Realizado por:";
        ws.Cell(fo + 5, 1).Style.Font.Bold = true;
        ws.Cell(fo + 5, 4).Value = responsable ?? "";

        ws.Columns().AdjustToContents();
        ws.Column(1).Width = Math.Min(ws.Column(1).Width, 42);
    }

    private static void HojaDetalles(XLWorkbook wb, IReadOnlyList<ReporteProveedorMatrizDto> reportes)
    {
        var ws = wb.Worksheets.Add("DETALLES");
        ws.Cell("A1").Value = "AGREGUE AQUI EL DETALLE CORRESPONDIENTE A CADA TICKET DE SU SISTEMA";
        ws.Range("A1:E1").Merge();
        ws.Cell("A1").Style.Font.Italic = true;
        ws.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#667085");

        string[] cab = { "Ticket EIKON", "Titulo", "Detalle", "Solucion", "Status" };
        for (int i = 0; i < cab.Length; i++)
        {
            var c = ws.Cell(2, i + 1);
            c.Value = cab[i];
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = Azul;
            c.Style.Font.FontColor = XLColor.White;
        }

        int fila = 3;
        foreach (var r in reportes)
        {
            ws.Cell(fila, 1).Value = r.NumeroCasoExterno ?? r.RefNumSistema ?? "";
            ws.Cell(fila, 2).Value = r.Titulo ?? "";
            ws.Cell(fila, 3).Value = r.Descripcion ?? r.ContextoEnviado ?? "";
            ws.Cell(fila, 4).Value = r.ResultadoSolucion ?? r.RespuestasRecibidas ?? "";
            ws.Cell(fila, 5).Value = r.EstadoTexto;
            ws.Range(fila, 1, fila, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(fila, 1, fila, 5).Style.Alignment.WrapText = true;
            ws.Range(fila, 1, fila, 5).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            fila++;
        }

        ws.Column(1).Width = 14;
        ws.Column(2).Width = 34;
        ws.Column(3).Width = 46;
        ws.Column(4).Width = 46;
        ws.Column(5).Width = 18;
    }

    private static void Etiqueta(IXLWorksheet ws, string celda, string texto)
    {
        ws.Cell(celda).Value = texto;
        ws.Cell(celda).Style.Font.Bold = true;
    }
}
