using FastEndpoints;
using PaylKoyn.Data.Responses;
using PaylKoyn.Node.Data;
using PaylKoyn.Node.Services;

namespace PaylKoyn.Node.Endpoints;

public class UploadRequest(WalletService walletService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/upload/request/{address}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? airdropAddress = Route<string>("address");

        Wallet wallet = await walletService.GenerateWalletAsync(airdropAddress);
        string address = wallet.Address!;

        await SendOkAsync(new UploadRequestResponse(address), ct);
    }
}