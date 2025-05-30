using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using PaylKoyn.Web.Services;

namespace PaylKoyn.Web.Components.Pages;

public partial class Upload
{
    [Inject]
    public required IconService IconService { get; set; }

    protected readonly List<IBrowserFile> FileList = [];
    protected bool IsOnDragged = false;

    protected void OnInputFileChanged(InputFileChangeEventArgs e)
    {
        FileList.AddRange(e.GetMultipleFiles());
    }

}