using Microsoft.AspNetCore.Components;
using PaylKoyn.Web.Services;

namespace PaylKoyn.Web.Components.Pages.Home;

public partial class Home
{
    [Inject]
    public required NavigationManager Navigation { get; set; }

    #region Open Graph Tags

    protected string OgUrl => $"{Navigation.BaseUri}{Navigation.ToBaseRelativePath(Navigation.Uri)}";
    protected string OgImageUrl => $"{Navigation.BaseUri}images/paylkoyn_og.webp";

    #endregion
}