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
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "260px"
        }
    };
}
