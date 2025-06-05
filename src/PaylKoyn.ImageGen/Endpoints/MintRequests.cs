using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Data.Responses;

namespace PaylKoyn.ImageGen.Endpoints;

public class MintRequestsDetails(IDbContextFactory<MintDbContext> dbContextFactory) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/mint/requests/{address}");
        AllowAnonymous();
        Description(x => x
            .WithTags("NFT")
            .WithSummary("Fetches details of a mint request by user address")
            .WithDescription("Gets a mint requests by user address from the URL path."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string address = Route<string>("address")!;

        await using MintDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            IEnumerable<MintRequest>? mintRequests = await dbContext.MintRequests
                .AsNoTracking()
                .Where(mr => mr.UserAddress == address)
                .ToListAsync(ct);

            if (mintRequests is null || !mintRequests.Any())
            {
                await SendNotFoundAsync(ct);
                return;
            }

            List<MintRequestDetailsResponse> response = [.. mintRequests.Select(mr => new MintRequestDetailsResponse(
                mr.Address!,
                mr.Status.ToString(),
                mr.UploadPaymentAddress ?? string.Empty,
                mr.AdaFsId,
                mr.MintTxHash,
                (mr.Image?.Length ?? 1) / 1024m / 1024m,
                mr.UpdatedAt
            ))];

            await SendOkAsync(response, ct);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving mint requests. Address: {Address}", address);
            ThrowError("An unexpected error occurred while retrieving mint requests.");
        }
    }
}