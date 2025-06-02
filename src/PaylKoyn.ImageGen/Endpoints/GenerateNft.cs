using FastEndpoints;
using Paylkoyn.ImageGen.Services;

namespace PaylKoyn.ImageGen.Endpoints;

public class GenerateNft(NftRandomizerService nftRandomizerService) : Endpoint<List<NftTrait>>
{
    public override void Configure()
    {
        Post("/generate/nft");
        AllowAnonymous();
        Description(x => x
            .WithTags("NFT")
            .WithSummary("Generates a new NFT")
            .WithDescription("This endpoint generates an NFT image based on the provided traits."));
    }

    public override async Task HandleAsync(List<NftTrait> req, CancellationToken ct)
    {
        byte[] result = nftRandomizerService.GenerateRandomNFT(req);
        string fileName = $"nft_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";

        using MemoryStream stream = new(result);
        await SendStreamAsync(stream, fileName, cancellation: ct);
    }
}