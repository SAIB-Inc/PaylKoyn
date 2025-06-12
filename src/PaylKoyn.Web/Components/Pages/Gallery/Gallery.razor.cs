using Microsoft.AspNetCore.Components;
using PaylKoyn.Web.Services;

namespace PaylKoyn.Web.Components.Pages;

public partial class Gallery
{
    [Inject]
    public required IconService IconService { get; set; }
    
    [Inject]
    public required NavigationManager Navigation { get; set; }

    protected int Selected = 1;
    #region Open Graph Tags

    protected string OgUrl => $"{Navigation.BaseUri}{Navigation.ToBaseRelativePath(Navigation.Uri)}";
    protected string OgImageUrl => $"{Navigation.BaseUri}images/paylkoyn_og.webp";

    #endregion
}