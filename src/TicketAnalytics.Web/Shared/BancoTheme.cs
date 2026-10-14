using MudBlazor;

namespace TicketAnalytics.Web.Shared;

/// <summary>Tema institucional Banco Santa Cruz: azul #15528E + verde #76B82A.
/// Compatible con MudBlazor 7.x.</summary>
public static class BancoTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#15528E",
            Secondary = "#76B82A",
            Tertiary = "#0F3D6B",
            AppbarBackground = "#15528E",
            AppbarText = "#FFFFFF",
            Background = "#F5F7FA",
            Surface = "#FFFFFF",
            DrawerBackground = "#0F3D6B",
            DrawerText = "#E8EEF5",
            DrawerIcon = "#B9CBDF",
            Success = "#2E7D32",
            Warning = "#ED6C02",
            Error = "#C62828",
            Info = "#15528E",
            TextPrimary = "#1A2733",
            TextSecondary = "#5A6B7B"
        },
        // Modo noche: misma identidad (azul + verde) aclarada para leerse sobre fondo oscuro.
        PaletteDark = new PaletteDark
        {
            Primary = "#4C8FD0",
            Secondary = "#8BC94A",
            Tertiary = "#8DBBEA",
            AppbarBackground = "#0F2A47",
            AppbarText = "#FFFFFF",
            Background = "#0F1722",
            BackgroundGray = "#1B2738",
            Surface = "#172231",
            DrawerBackground = "#0B1F35",
            DrawerText = "#E8EEF5",
            DrawerIcon = "#B9CBDF",
            Success = "#5CB85F",
            Warning = "#FFA347",
            Error = "#EF5F5C",
            Info = "#4FA3EE",
            TextPrimary = "#E6EDF5",
            TextSecondary = "#A9B8C9",
            TextDisabled = "#5F7187",
            ActionDefault = "#A9B8C9",
            ActionDisabled = "#4A5B70",
            ActionDisabledBackground = "#223044",
            LinesDefault = "#2A3A50",
            LinesInputs = "#46596F",
            TableLines = "#2A3A50",
            TableStriped = "#1B2738",
            TableHover = "#1E2B3D",
            Divider = "#2A3A50",
            DividerLight = "#223044",
            OverlayDark = "rgba(5,10,18,.7)"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "260px"
        }
    };
}
