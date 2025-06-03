using System.Text.Json;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Network.Cbor.LocalStateQuery;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;
using FastEndpoints;
using Paylkoyn.ImageGen.Services;
using PaylKoyn.Data.Responses;
using PaylKoyn.Data.Services;
using PaylKoyn.ImageGen.Services;

namespace PaylKoyn.ImageGen.Endpoints;

public record MintNftRequest(string UserAddress);

public class MintNft(
    TransactionTemplateService templateService,
    NftRandomizerService nftRandomizerService,
    MintingService mintingService,
    IHttpClientFactory httpClientFactory
) : Endpoint<MintNftRequest>
{
    private readonly HttpClient _nodeClient = httpClientFactory.CreateClient("PaylKoynNodeClient");
    public override void Configure()
    {
        Post("/mint/nft");
        AllowAnonymous();
        Description(x => x
            .WithTags("NFT")
            .WithSummary("Mints a new NFT")
            .WithDescription("This endpoint mints a new NFT based on the provided user address, asset name, and metadata."));
    }

    public override async Task HandleAsync(MintNftRequest req, CancellationToken ct)
    {
        TransactionTemplate<MintNftParams> nftMintTempalte = templateService.MintNftTemplate();
        IEnumerable<NftTrait> randomTraits = nftRandomizerService.GenerateRandomTraits();
        // byte[] image = nftRandomizerService.GenerateNftImage(randomTraits);

        Chrysalis.Cbor.Types.Cardano.Core.Metadata metadata = mintingService.CreateNftMetadata(
            "999598abd25973216954178fbc179358753ff5bb6e860e16a05469b0",
            "6e6674",
            "b119507e859f08b32fc369e182ae6f555254c1c2acff88dad90e11d44c2699f5",
            [.. randomTraits],
            "NFT Test #1",
            "This is a randomly generated NFT"
        );
        Dictionary<string, int> mintAssets = new()
        {
            ["6e6674"] = 1
        };
        MintNftParams mintParams = new(req.UserAddress, req.UserAddress, "addr_test1qpcxqfg6xrzqus5qshxmgaa2pj5yv2h9mzm22hj7jct2ad59q2pfxagx7574360xl47vhw79wxtdtze2z83k5a4xpptsm6dhy7", metadata, mintAssets);
        Chrysalis.Cbor.Types.Cardano.Core.Transaction.Transaction tx = await nftMintTempalte(mintParams);
        string serialized = Convert.ToHexString(CborSerializer.Serialize(tx));

        await SendAsync(serialized, cancellation: ct);
    }
}