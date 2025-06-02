using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using PaylKoyn.Data.Responses;
using PaylKoyn.Web.Services;

namespace PaylKoyn.Web.Components.Pages;

public partial class Upload
{
    [Inject]
    public required IconService IconService { get; set; }
    
    [Inject]
    public required UploadService UploadService { get; set; }

    protected readonly List<UploadFileState> FileList = [];
    protected string? UploadAddress;

    protected override async Task OnInitializedAsync()
    {
        // Request upload address on page load
        var response = await UploadService.RequestUploadAsync();
        UploadAddress = response?.Id;
        StateHasChanged();
    }

    protected async Task OnInputFileChanged(InputFileChangeEventArgs e)
    {
        foreach (var file in e.GetMultipleFiles())
        {
            // Estimate fee for this file
            var feeResponse = await UploadService.EstimateFeeAsync(file.Size);
            var uploadState = new UploadFileState
            {
                File = file,
                EstimatedFee = feeResponse?.EstimatedFee ?? 0,
                Status = UploadStatus.Ready
            };
            
            FileList.Add(uploadState);
        }
        StateHasChanged();
    }

    protected async Task UploadFile(UploadFileState uploadState)
    {
        if (string.IsNullOrEmpty(UploadAddress)) return;

        uploadState.Status = UploadStatus.Uploading;
        StateHasChanged();

        var (isSuccess, message) = await UploadService.UploadFileAsync(UploadAddress, uploadState.File);
        
        uploadState.Status = isSuccess ? UploadStatus.Completed : UploadStatus.Error;
        uploadState.Message = message;
        StateHasChanged();
    }

    protected void RemoveFile(UploadFileState uploadState)
    {
        FileList.Remove(uploadState);
        StateHasChanged();
    }
}

public class UploadFileState
{
    public required IBrowserFile File { get; set; }
    public ulong EstimatedFee { get; set; }
    public UploadStatus Status { get; set; }
    public string Message { get; set; } = "";
}

public enum UploadStatus
{
    Ready,
    Uploading,
    Completed,
    Error
}