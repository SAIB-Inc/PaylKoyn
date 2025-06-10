using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Responses;
using PaylKoyn.Node.Data;

namespace PaylKoyn.Node.Endpoints;

public class RefundResponse
{
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
}

public class RefundRequest(
    IDbContextFactory<WalletDbContext> dbContextFactory,
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

        using WalletDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        Wallet? request = await dbContext.Wallets
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

    private RefundResponse ValidateRefundRequest(Wallet request)
    {
        return request.Status switch
        {
            UploadStatus.Waiting when request.CreatedAt + _requestExpirationTime > DateTime.UtcNow
                => new() { Message = "Refund request is not allowed. Request has not expired yet.", Success = false },

            UploadStatus.Failed when request.LastValidStatus != UploadStatus.Waiting
                => new() { Message = "Refund request is not allowed for this mint request status.", Success = false },

            UploadStatus.RefundRequested
                => new() { Message = "Refund already requested for this mint request.", Success = false },

            UploadStatus.Refunded
                => new() { Message = "Refund already processed for this mint request.", Success = false },

            UploadStatus.Waiting or UploadStatus.Failed
                => new() { Message = "Refund request validation passed.", Success = true },

            _ => new() { Message = "Refund request is not allowed for this mint request status.", Success = false }
        };
    }

    private async Task ProcessRefundRequest(Wallet request, WalletDbContext dbContext, CancellationToken ct)
    {
        request.Status = UploadStatus.RefundRequested;
        dbContext.Wallets.Update(request);
        await dbContext.SaveChangesAsync(ct);

        RefundResponse response = new()
        {
            Message = "Refund request has been successfully submitted.",
            Success = true
        };

        await SendOkAsync(response, ct);
    }
}