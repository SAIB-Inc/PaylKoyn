using Microsoft.AspNetCore.Components;

namespace PaylKoyn.Web.Components.Pages.Buy;

public partial class Buy
{
    [Inject]
    public required NavigationManager Navigation { get; set; }

    #region Open Graph Tags

    protected string OgUrl => $"{Navigation.BaseUri}{Navigation.ToBaseRelativePath(Navigation.Uri)}";
    protected string OgImageUrl => $"{Navigation.BaseUri}images/paylkoyn_og.webp";

    #endregion
}