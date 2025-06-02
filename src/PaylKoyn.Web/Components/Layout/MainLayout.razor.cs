using MudBlazor;

namespace PaylKoyn.Web.Components.Layout;
public partial class MainLayout
{
    public MudTheme PaylKoynTheme => new()
    {
        PaletteLight = new()
        {
            Background = "#5438DC",
            Primary = "#FFFFFF",
            PrimaryLighten = "#FAFAFA",
            Secondary = "#7263FA",
            SecondaryLighten = "#998EFF",
            SecondaryDarken = "#4B41A7",
            Tertiary = "#8E82FF",
            TextPrimary = "#0F051D",
            TextSecondary = "#7B7583",
            GrayLight = "#EFF1FA",
            GrayDark = "#CBD0DC",
            GrayDarker = "#EEF1F7"
        },
        Typography = new()
        {
            H1 = new H1Typography()
            {
                FontFamily = [ "Oxanium" ],
                FontSize = "64px",
                FontWeight = "700",
                LineHeight = "72px",
                LetterSpacing = "0%"
            },
            H2 = new H2Typography()
            {
                FontFamily = [ "Oxanium" ],
                FontSize = "56px",
                FontWeight = "800",
                LineHeight = "72px",
                LetterSpacing = "0%"
            },
            Default = new DefaultTypography()
            {
                FontFamily = ["Inter"]
            }
        }

    };
}