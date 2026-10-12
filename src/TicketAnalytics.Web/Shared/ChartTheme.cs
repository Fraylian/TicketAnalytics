using ApexCharts;

namespace TicketAnalytics.Web.Shared;

/// <summary>
/// Una sola fuente de estilo para todos los gráficos ApexCharts: tipografía,
/// grilla, animación de entrada, tooltip, estados hover/activo y paleta institucional.
/// Cada página pide un tipo (barras, área, dona, apilada) y obtiene la misma estética,
/// en vez de repetir opciones sueltas con colores distintos en cada pantalla.
/// </summary>
public static class ChartTheme
{
    public const string Azul = "#15528E";
    public const string AzulProfundo = "#0F3D6B";
    public const string AzulClaro = "#5A9BD5";
    public const string Verde = "#76B82A";
    public const string Exito = "#2E7D32";
    public const string Ambar = "#ED6C02";
    public const string Rojo = "#C62828";
    public const string Violeta = "#6B4EFF";
    public const string Gris = "#8FA3B8";

    /// <summary>Paleta categórica: institucional primero, luego tonos diferenciables.</summary>
    public static readonly List<string> Paleta = new()
    {
        "#15528E", "#76B82A", "#00897B", "#6B4EFF", "#C75100", "#7B1FA2", "#455A64", "#AD1457", "#5A9BD5"
    };

    private const string Fuente = "'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

    /// <summary>Opciones comunes a todos los gráficos.</summary>
    public static ApexChartOptions<T> Base<T>(List<string>? colores = null) where T : class => new()
    {
        Chart = new Chart
        {
            Toolbar = new Toolbar { Show = false },
            FontFamily = Fuente,
            ForeColor = "#5A6B7B",
            Animations = new Animations
            {
                Enabled = true,
                Easing = Easing.Easeinout,
                Speed = 750,
                AnimateGradually = new AnimateGradually { Enabled = true, Delay = 80 },
                DynamicAnimation = new DynamicAnimation { Enabled = true, Speed = 420 }
            }
        },
        Colors = colores ?? Paleta,
        Grid = new Grid
        {
            BorderColor = "#E4E9F0",
            StrokeDashArray = 4,
            Padding = new Padding { Left = 6, Right = 10 }
        },
        DataLabels = new DataLabels { Enabled = false },
        Legend = new Legend
        {
            Position = LegendPosition.Top,
            HorizontalAlign = Align.Left,
            FontSize = "12px",
            FontWeight = 600,
            ItemMargin = new LegendItemMargin { Horizontal = 10, Vertical = 4 }
        },
        Tooltip = new Tooltip { Enabled = true, Style = new TooltipStyle { FontSize = "12px", FontFamily = Fuente } },
        States = new States
        {
            Hover = new StatesHover { Filter = new StatesFilter { Type = StatesFilterType.darken, Value = 0.92 } },
            Active = new StatesActive { Filter = new StatesFilter { Type = StatesFilterType.darken, Value = 0.82 } }
        },
        Yaxis = new List<YAxis> { new YAxis { DecimalsInFloat = 0, ForceNiceScale = true } }
    };

    /// <summary>Barras verticales u horizontales con esquinas redondeadas.</summary>
    public static ApexChartOptions<T> Barras<T>(List<string>? colores = null, bool horizontal = false,
                                                bool distribuidas = false, bool etiquetas = false) where T : class
    {
        var o = Base<T>(colores);
        o.PlotOptions = new PlotOptions
        {
            Bar = new PlotOptionsBar
            {
                Horizontal = horizontal,
                BorderRadius = 5,
                BorderRadiusApplication = BorderRadiusApplication.End,
                ColumnWidth = "58%",
                BarHeight = "64%",
                Distributed = distribuidas
            }
        };
        // Trazo transparente: separa visualmente las series agrupadas.
        o.Stroke = new Stroke { Show = true, Width = 2, Colors = new List<string> { "transparent" } };
        if (etiquetas)
            o.DataLabels = new DataLabels
            {
                Enabled = true,
                Style = new DataLabelsStyle { FontSize = "11px", FontWeight = 600, Colors = new List<string> { "#fff" } }
            };
        if (distribuidas) o.Legend = new Legend { Show = false };
        if (horizontal)
        {
            o.Xaxis = new XAxis { DecimalsInFloat = 0 };
            o.Yaxis = new List<YAxis> { new YAxis { Labels = new YAxisLabels { MaxWidth = 170, MinWidth = 60 } } };
            o.Grid.Padding = new Padding { Left = 14, Right = 16 };
        }
        return o;
    }

    /// <summary>Barras apiladas; con porcentaje=true cada barra suma 100%.</summary>
    public static ApexChartOptions<T> Apiladas<T>(List<string> colores, bool horizontal = true, bool porcentaje = false) where T : class
    {
        var o = Barras<T>(colores, horizontal);
        o.Chart.Stacked = true;
        if (porcentaje) o.Chart.StackType = StackType.Percent100;
        o.PlotOptions.Bar.BorderRadiusWhenStacked = BorderRadiusWhenStacked.Last;
        o.Stroke = new Stroke { Show = true, Width = 1, Colors = new List<string> { "#fff" } };
        return o;
    }

    /// <summary>Área suave con degradado: ideal para tendencias y flujo.</summary>
    public static ApexChartOptions<T> Area<T>(List<string>? colores = null) where T : class
    {
        var o = Base<T>(colores);
        o.Stroke = new Stroke { Curve = Curve.Smooth, Width = 3 };
        o.Fill = new Fill
        {
            Type = FillType.Gradient,
            Gradient = new FillGradient
            {
                ShadeIntensity = 1,
                OpacityFrom = 0.38,
                OpacityTo = 0.03,
                Stops = new List<double> { 0, 92, 100 }
            }
        };
        o.Markers = new Markers { Size = 0, StrokeWidth = 2, StrokeColors = "#fff" };
        o.Tooltip.Shared = true;
        o.Tooltip.Intersect = false;
        return o;
    }

    /// <summary>Líneas suaves con marcadores.</summary>
    public static ApexChartOptions<T> Lineas<T>(List<string>? colores = null) where T : class
    {
        var o = Base<T>(colores);
        o.Stroke = new Stroke { Curve = Curve.Smooth, Width = 3 };
        o.Markers = new Markers { Size = 4, StrokeWidth = 2, StrokeColors = "#fff" };
        o.Tooltip.Shared = true;
        o.Tooltip.Intersect = false;
        return o;
    }

    /// <summary>Dona con total al centro y leyenda inferior.</summary>
    public static ApexChartOptions<T> Dona<T>(List<string>? colores = null, string etiquetaTotal = "Total") where T : class
    {
        var o = Base<T>(colores);
        o.Legend = new Legend
        {
            Position = LegendPosition.Bottom,
            HorizontalAlign = Align.Center,
            FontSize = "12px",
            ItemMargin = new LegendItemMargin { Horizontal = 8, Vertical = 3 }
        };
        o.Stroke = new Stroke { Show = true, Width = 2, Colors = new List<string> { "#fff" } };
        o.PlotOptions = new PlotOptions
        {
            Pie = new PlotOptionsPie
            {
                ExpandOnClick = true,
                Donut = new PlotOptionsDonut
                {
                    Size = "68%",
                    Labels = new DonutLabels
                    {
                        Show = true,
                        Name = new DonutLabelName { Show = true, FontSize = "12px", Color = "#5A6B7B" },
                        Value = new DonutLabelValue { Show = true, FontSize = "24px", FontWeight = 700, Color = "#1A2733" },
                        Total = new DonutLabelTotal
                        {
                            Show = true,
                            ShowAlways = true,
                            Label = etiquetaTotal,
                            FontSize = "12px",
                            Color = "#5A6B7B"
                        }
                    }
                }
            }
        };
        o.Yaxis = null;
        return o;
    }
}
