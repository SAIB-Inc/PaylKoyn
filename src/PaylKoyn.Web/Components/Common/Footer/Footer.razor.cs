using Microsoft.AspNetCore.Components;
using PaylKoyn.Web.Services;

namespace PaylKoyn.Web.Components.Common.Footer;

public partial class Footer
{
    [Inject]
    public required IconService IconService { get; set; }
}