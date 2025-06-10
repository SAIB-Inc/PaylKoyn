using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.ImageGen.Data;

namespace PaylKoyn.ImageGen.Endpoints;

public class RefundResponse
{
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
}

public class RefundRequest(
    IDbContextFactory<MintDbContext> dbContextFactory,
    IConfiguration configuration
) : EndpointWithoutRequest
{
    private readonly TimeSpan _requestExpirationTime = TimeSpan.FromMinutes(configuration.GetValue("RequestExpirationMinutes", 30));

    public override void Configure()
    {
        Get("/refund/request/{address}");
        AllowAnonymous();
        Description(x => x
            .WithTags("Refund")
            .WithSummary("Requests for a refund by passing the temporary user generated address")
            .WithDescription("This endpoint allows users to request a refund by providing their temporary address in the URL path."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string address = Route<string>("address", isRequired: true)!;

        using MintDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        MintRequest? request = await dbContext.MintRequests
            .FirstOrDefaultAsync(mr => mr.Address == address, ct);

        if (request is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        RefundResponse response = ValidateRefundRequest(request);

        if (!response.Success)
        {
            await SendAsync(response, 400, ct);
            return;
        }

        await ProcessRefundRequest(request, dbContext, ct);
    }

    private RefundResponse ValidateRefundRequest(MintRequest request)
    {
        return request.Status switch
        {
            MintStatus.Waiting when request.CreatedAt + _requestExpirationTime > DateTime.UtcNow
                => new() { Message = "Refund request is not allowed. Request has not expired yet.", Success = false },

            MintStatus.Failed when request.LastValidStatus != MintStatus.Waiting
                => new() { Message = "Refund request is not allowed for this mint request status.", Success = false },

            MintStatus.RefundRequested
                => new() { Message = "Refund already requested for this mint request.", Success = false },

            MintStatus.Refunded
                => new() { Message = "Refund already processed for this mint request.", Success = false },

            MintStatus.Waiting or MintStatus.Failed
                => new() { Message = "Refund request validation passed.", Success = true },

            _ => new() { Message = "Refund request is not allowed for this mint request status.", Success = false }
        };
    }

    private async Task ProcessRefundRequest(MintRequest request, MintDbContext dbContext, CancellationToken ct)
    {
        request.Status = MintStatus.RefundRequested;
        dbContext.MintRequests.Update(request);
        await dbContext.SaveChangesAsync(ct);

        RefundResponse response = new()
        {
            Message = "Refund request has been successfully submitted.",
            Success = true
        };

        await SendOkAsync(response, ct);
    }
}