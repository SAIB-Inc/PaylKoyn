using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Words;
using FastEndpoints;
using PaylKoyn.ImageGen.Services;

namespace PaylKoyn.ImageGen.Endpoints;

public class GenerateMnemonic : Endpoint<List<NftTrait>>
{
    public override void Configure()
    {
        Post("/generate/mnemonic");
        AllowAnonymous();
        Description(x => x
            .WithTags("NFT")
            .WithSummary("Generates a new mnemonic")
        );
    }

    public override async Task HandleAsync(List<NftTrait> req, CancellationToken ct)
    {
        var mnemonic = Mnemonic.Generate(English.Words, 24);
        await SendOkAsync(string.Join(" ", mnemonic.Words), cancellation: ct);
    }
}