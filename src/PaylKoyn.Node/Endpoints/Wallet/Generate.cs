using FastEndpoints;
using PaylKoyn.Node.Services;

namespace PaylKoyn.Node.Endpoints.Wallet;

public class Test(WalletService walletService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/wallet/generate");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        PaylKoyn.Data.Models.Wallet wallet = await walletService.GenerateWalletAsync();
        string address = wallet.Address;

        await SendOkAsync(address, ct);
    }
}