using ClosedXML.Excel;
using TicketAnalytics.Application.ReporteVP.DTOs;

namespace TicketAnalytics.API.Services;

/// <summary>
/// Excel del reporte mensual al VP. Dos hojas: Resumen Ejecutivo (KPIs y cortes)
/// y Detalle (todos los casos filtrados, con autofilter y panes congelados).
/// </summary>
public static class ReporteVPExcel
{
    private static readonly XLColor Azul = XLColor.FromHtml("#15528E");
    private static readonly XLColor AzulSuave = XLColor.FromHtml("#EAF2F9");
    private static readonly XLColor Gris = XLColor.FromHtml("#667085");

    public static byte[] Generar(ReporteVPOverviewDto d)
    {
        using var wb = new XLWorkbook();
        HojaResumen(wb, d);
        HojaDetalle(wb, d);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void HojaResumen(XLWorkbook wb, ReporteVPOverviewDto d)
    {
        var ws = wb.Worksheets.Add("Resumen Ejecutivo");
        ws.Style.Font.FontName = "Calibri";

        ws.Cell("A1").Value = "REPORTE MENSUAL AL VP";
        ws.Range("A1:F1").Merge();
        Titulo(ws.Cell("A1"));
        ws.Row(1).Height = 26;

        ws.Cell("A2").Value = "Período:";      ws.Cell("A2").Style.Font.Bold = true;
        ws.Cell("B2").Value = d.PeriodoTexto;
        ws.Cell("A3").Value = "Generado:";     ws.Cell("A3").Style.Font.Bold = true;
        ws.Cell("B3").Value = d.GeneradoEn;    ws.Cell("B3").Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        ws.Cell("A4").Value = "Total de casos:"; ws.Cell("A4").Style.Font.Bold = true;
        ws.Cell("B4").Value = d.TotalCasos;

        // Resumen narrativo
        ws.Cell("A6").Value = "Resumen";
        ws.Cell("A6").Style.Font.Bold = true;
        ws.Cell("A7").Value = d.ResumenEjecutivo;
        ws.Range("A7:F9").Merge();
        ws.Cell("A7").Style.Alignment.WrapText = true;
        ws.Cell("A7").Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        ws.Cell("A7").Style.Fill.BackgroundColor = AzulSuave;

        // KPIs
        int f = 11;
        ws.Cell(f, 1).Value = "INDICADOR";
        ws.Cell(f, 2).Value = "VALOR";
        ws.Cell(f, 3).Value = "MES ANTERIOR";
        ws.Cell(f, 4).Value = "VARIACIÓN";
        Encabezado(ws.Range(f, 1, f, 4));

        foreach (var k in d.Kpis)
        {
            f++;
            ws.Cell(f, 1).Value = k.Titulo;
            ws.Cell(f, 2).Value = k.Valor;
            if (k.ValorAnterior.HasValue) ws.Cell(f, 3).Value = k.ValorAnterior.Value;
            if (k.Delta is int delta)
            {
                ws.Cell(f, 4).Value = delta > 0 ? $"+{delta}" : delta.ToString();
                ws.Cell(f, 4).Style.Font.FontColor = delta > 0
                    ? XLColor.FromHtml("#2E7D32") : delta < 0 ? XLColor.FromHtml("#C62828") : Gris;
            }
            ws.Range(f, 1, f, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Por categoría
        f += 2;
        ws.Cell(f, 1).Value = "POR CATEGORÍA";
        ws.Cell(f, 2).Value = "CASOS";
        ws.Cell(f, 3).Value = "% DEL PERÍODO";
        Encabezado(ws.Range(f, 1, f, 3));
        foreach (var c in d.Categorias)
        {
            f++;
            ws.Cell(f, 1).Value = c.Categoria;
            ws.Cell(f, 2).Value = c.Total;
            ws.Cell(f, 3).Value = d.TotalCasos == 0 ? 0 : (double)c.Total / d.TotalCasos;
            ws.Cell(f, 3).Style.NumberFormat.Format = "0.0%";
            ws.Range(f, 1, f, 3).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Por estado
        f += 2;
        ws.Cell(f, 1).Value = "POR ESTADO";
        ws.Cell(f, 2).Value = "CASOS";
        ws.Cell(f, 3).Value = "% DEL PERÍODO";
        Encabezado(ws.Range(f, 1, f, 3));
        foreach (var e in d.PorEstado)
        {
            f++;
            ws.Cell(f, 1).Value = e.Nombre;
            ws.Cell(f, 1).Style.Font.FontColor = XLColor.FromHtml(e.Color);
            ws.Cell(f, 1).Style.Font.Bold = true;
            ws.Cell(f, 2).Value = e.Cantidad;
            ws.Cell(f, 3).Value = e.Porcentaje / 100.0;
            ws.Cell(f, 3).Style.NumberFormat.Format = "0.0%";
            ws.Range(f, 1, f, 3).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        ws.Column(1).Width = 34;
        ws.Column(2).Width = 14;
        ws.Column(3).Width = 16;
        ws.Column(4).Width = 14;
        ws.Columns(5, 6).Width = 18;
    }

    private static void HojaDetalle(XLWorkbook wb, ReporteVPOverviewDto d)
    {
        var ws = wb.Worksheets.Add("Detalle");
        ws.Style.Font.FontName = "Calibri";

        string[] cab =
        {
            "Caso", "Tipo", "Resumen", "Categoría VP", "Estado VP", "Categoría origen",
            "Responsable", "Proveedor", "JIRA", "Última actualización", "Nota ejecutiva"
        };
        for (int i = 0; i < cab.Length; i++) ws.Cell(1, i + 1).Value = cab[i];
        Encabezado(ws.Range(1, 1, 1, cab.Length));

        int f = 1;
        foreach (var c in d.Casos)
        {
            f++;
            ws.Cell(f, 1).Value = c.RefNum;
            ws.Cell(f, 2).Value = c.TipoTexto;
            ws.Cell(f, 3).Value = c.Resumen ?? "";
            ws.Cell(f, 4).Value = c.CategoriaVP;
            ws.Cell(f, 5).Value = c.EstadoTexto;
            ws.Cell(f, 5).Style.Font.FontColor = XLColor.FromHtml(c.EstadoColor);
            ws.Cell(f, 5).Style.Font.Bold = true;
            ws.Cell(f, 6).Value = c.CategoriaOrigen ?? "";
            ws.Cell(f, 7).Value = c.Responsable ?? "";
            ws.Cell(f, 8).Value = c.Proveedor ?? c.ReportadoProveedorTexto;
            ws.Cell(f, 9).Value = c.Jira ?? "";
            if (c.UltimaActualizacion.HasValue)
            {
                ws.Cell(f, 10).Value = c.UltimaActualizacion.Value;
                ws.Cell(f, 10).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
            }
            ws.Cell(f, 11).Value = c.NotaEjecutiva ?? "";
            ws.Range(f, 1, f, cab.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Hair;
            ws.Range(f, 1, f, cab.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            ws.Cell(f, 3).Style.Alignment.WrapText = true;
            ws.Cell(f, 11).Style.Alignment.WrapText = true;
        }

        if (f >= 1)
        {
            ws.Range(1, 1, Math.Max(f, 2), cab.Length).SetAutoFilter();
            ws.SheetView.FreezeRows(1);
        }

        ws.Column(1).Width = 13;
        ws.Column(2).Width = 15;
        ws.Column(3).Width = 52;
        ws.Column(4).Width = 24;
        ws.Column(5).Width = 15;
        ws.Column(6).Width = 30;
        ws.Column(7).Width = 24;
        ws.Column(8).Width = 20;
        ws.Column(9).Width = 16;
        ws.Column(10).Width = 20;
        ws.Column(11).Width = 46;
    }

    private static void Titulo(IXLCell c)
    {
        c.Style.Font.Bold = true;
        c.Style.Font.FontSize = 14;
        c.Style.Font.FontColor = XLColor.White;
        c.Style.Fill.BackgroundColor = Azul;
        c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void Encabezado(IXLRange r)
    {
        r.Style.Font.Bold = true;
        r.Style.Font.FontColor = XLColor.White;
        r.Style.Fill.BackgroundColor = Azul;
        r.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        r.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    }
}
