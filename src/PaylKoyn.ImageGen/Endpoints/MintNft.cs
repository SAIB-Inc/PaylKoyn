using FastEndpoints;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Services;

namespace PaylKoyn.ImageGen.Endpoints;

public record MintNftRequest(string UserAddress);

public class MintNft(WalletService walletService) : Endpoint<MintNftRequest>
{
    public override void Configure()
    {
        Post("/mint/request");
        AllowAnonymous();
        Description(x => x
            .WithTags("NFT")
            .WithSummary("Creates a mint request for an NFT")
            .WithDescription("Creates a mint request and returns the mint request address for the fee."));
    }

    public override async Task HandleAsync(MintNftRequest req, CancellationToken ct)
    {
        if (!ValidateAddress(req.UserAddress))
        {
            await SendErrorsAsync(400, ct);
        }
        else
        {
            MintRequest mintRequest = await walletService.GenerateMintRequestAsync(req.UserAddress);
            await SendAsync(mintRequest.Id, cancellation: ct);
        }
    }

    private static bool ValidateAddress(string address) =>
        !string.IsNullOrEmpty(address) && (address.StartsWith("addr1") || address.StartsWith("addr_test1"));

}