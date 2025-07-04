using FastEndpoints;
using PaylKoyn.Data.Responses;
using PaylKoyn.Node.Data;
using PaylKoyn.Node.Services;

namespace PaylKoyn.Node.Endpoints;

public class RetrieveUploadDetails(WalletService walletService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/upload/details/{address}");
        AllowAnonymous();
        Description(x => x
            .WithTags("Upload")
            .WithSummary("Fetches details of an upload request by address")
            .WithDescription("Gets the upload request details for a specific wallet address from the URL path."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? address = Route<string>("address");

        if (string.IsNullOrWhiteSpace(address))
        {
            await SendAsync(new { error = "Address is required." }, 400, cancellation: ct);
            return;
        }

        try
        {
            Wallet? wallet = await walletService.GetWalletAsync(address);

            if (wallet == null)
            {
                await SendAsync(new { error = "Wallet not found for address: " + address }, 404, cancellation: ct);
                return;
            }

            UploadDetailsResponse uploadDetails = new(
                wallet.Address!,
                wallet.AdaFsId,
                wallet.RefundTxHash,
                wallet.FileSize,
                wallet.Status.ToString().ToLowerInvariant(),
                wallet.UpdatedAt
            );

            await SendOkAsync(uploadDetails, cancellation: ct);

        }
        catch (Exception ex)
        {
            await SendAsync(new { error = "An error occurred while fetching the upload request: " + ex.Message }, 500, cancellation: ct);
            return;
        }
    }
}