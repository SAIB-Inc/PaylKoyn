using System.Text.Json;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Network.Cbor.LocalStateQuery;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Paylkoyn.ImageGen.Services;
using PaylKoyn.Data.Responses;
using PaylKoyn.Data.Services;
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
        MintRequest mintRequest = await walletService.GenerateMintRequestAsync(req.UserAddress);

        await SendAsync(mintRequest.Id, cancellation: ct);
    }
}