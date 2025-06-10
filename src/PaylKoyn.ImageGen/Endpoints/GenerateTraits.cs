using FastEndpoints;
using PaylKoyn.ImageGen.Services;

namespace PaylKoyn.ImageGen.Endpoints;

public class GenerateTraits(NftRandomizerService nftRandomizerService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/generate/traits");
        AllowAnonymous();
        Description(x => x
            .WithTags("Generate")
            .WithSummary("Generates new random NFT traits")
            .WithDescription("This endpoints is used to generate a combination of traits for an NFT."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        IEnumerable<NftTrait> result = nftRandomizerService.GenerateRandomTraits();
        await SendAsync(result, cancellation: ct);
    }
}