using FastEndpoints;
using PaylKoyn.Data.Models;
using PaylKoyn.Node.Services;

namespace PaylKoyn.Node.Endpoints;

public record UploadRequestResponse(string Id);

public class UploadRequest(WalletService walletService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/upload/request");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Wallet wallet = await walletService.GenerateWalletAsync();
        string address = wallet.Address;

        await SendOkAsync(new UploadRequestResponse(address), ct);
    }
}