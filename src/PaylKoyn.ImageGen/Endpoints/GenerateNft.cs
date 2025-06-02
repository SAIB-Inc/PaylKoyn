using FastEndpoints;
using Paylkoyn.ImageGen.Services;

namespace PaylKoyn.ImageGen.Endpoints;

public record GenerateNftRequest(IEnumerable<NftTrait> Traits, string? FileName = null, string? OutputPath = null);

public class GenerateNft(NftRandomizerService nftRandomizerService) : Endpoint<GenerateNftRequest>
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

    public override async Task HandleAsync(GenerateNftRequest req, CancellationToken ct)
    {
        byte[] result = nftRandomizerService.GenerateRandomNFT(req.Traits);
        string fileName = req.FileName ?? $"nft_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";

        using MemoryStream stream = new(result);
        await SendStreamAsync(stream, fileName, cancellation: ct);
    }
}