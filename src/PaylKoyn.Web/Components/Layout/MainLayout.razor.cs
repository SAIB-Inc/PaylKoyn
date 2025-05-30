using MudBlazor;

namespace PaylKoyn.Web.Components.Layout;

public partial class MainLayout
{
    public MudTheme PaylKoynTheme => new()
    {
        PaletteDark = new()
        {
            Background = "#000000"
        },
        PaletteLight = new()
        {
            Background = "#5438DC"
        }
    };
}