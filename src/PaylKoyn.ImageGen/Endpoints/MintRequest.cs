using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Data.Responses;

namespace PaylKoyn.ImageGen.Endpoints;

public class MintRequestDetails(IDbContextFactory<MintDbContext> dbContextFactory) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/mint/request/{id}");
        AllowAnonymous();
        Description(x => x
            .WithTags("Mint")
            .WithSummary("Fetches details of a mint request")
            .WithDescription("Gets a mint request by its ID from the URL path."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string id = Route<string>("id")!;

        await using MintDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            MintRequest? mintRequest = await dbContext.MintRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Address == id, ct);

            if (mintRequest is null)
            {
                await SendNotFoundAsync(ct);
                return;
            }

            MintRequestDetailsResponse response = new(
                mintRequest.Address!,
                mintRequest.Status.ToString().ToLowerInvariant(),
                mintRequest.UploadPaymentAddress ?? string.Empty,
                mintRequest.AdaFsId,
                mintRequest.MintTxHash,
                mintRequest.AirdropTxHash,
                mintRequest.RefundTxHash,
                (mintRequest.Image?.Length ?? 1) / 1024m / 1024m,
                mintRequest.UpdatedAt
            );

            await SendOkAsync(response, ct);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving mint request details. ID: {Id}", id);
            ThrowError("An unexpected error occurred while retrieving mint request details.");
        }
    }
}