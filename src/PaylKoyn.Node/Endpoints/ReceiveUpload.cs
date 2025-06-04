using FastEndpoints;
using Microsoft.AspNetCore.Mvc;
using PaylKoyn.Data.Services;
using PaylKoyn.Node.Services;
using Chrysalis.Wallet.Models.Keys;
using PaylKoyn.Data.Responses;
namespace PaylKoyn.Node.Endpoints;

public class UploadFileRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public IFormFile File { get; set; } = null!;
}

[RequestSizeLimit(100_000_000)]
public class ReceiveUpload(FileService fileService, WalletService walletService) : Endpoint<UploadFileRequest>
{
    public override void Configure()
    {
        Post("/upload/receive");
        AllowAnonymous();
        AllowFileUploads();
        Description(x => x
            .WithTags("Upload")
            .WithSummary("Receive an upload")
            .WithDescription("This endpoint is used to receive an upload from the client."));
    }

    public override async Task HandleAsync(UploadFileRequest req, CancellationToken ct)
    {
        Console.WriteLine($"Received file upload: Id={req.Id}, Name={req.Name}, ContentType={req.ContentType}, FileSize={req.File.Length} bytes");

        // Convert IFormFile to byte array
        byte[] fileContent;
        using (MemoryStream memoryStream = new MemoryStream())
        {
            await req.File.CopyToAsync(memoryStream, ct);
            fileContent = memoryStream.ToArray();
        }

        // Get private key for the wallet address (req.Id is the wallet address)
        PrivateKey? privateKey = await walletService.GetPrivateKeyByAddressAsync(req.Id);
        if (privateKey == null)
        {
            await SendAsync(new { error = "Wallet not found for address: " + req.Id }, 404, cancellation: ct);
            return;
        }

        try
        {
            string adaFsId = await fileService.UploadAsync(req.Id, fileContent, req.ContentType, req.Name, privateKey);
            await SendOkAsync(new UploadFileResponse(
                Message: "File will be uploaded soon.",
                AdaFsId: adaFsId,
                FileSize: fileContent.Length / 1024.0m / 1024.0m
            ), cancellation: ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading file: {ex.Message}");
            await SendAsync(new { error = ex.Message }, 500, cancellation: ct);
        }
    }
}