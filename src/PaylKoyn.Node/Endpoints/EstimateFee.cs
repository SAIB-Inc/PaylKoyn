using FastEndpoints;
using PaylKoyn.Data.Services;
using PaylKoyn.Data.Requests;
using PaylKoyn.Data.Responses;

namespace PaylKoyn.Node.Endpoints;

public class EstimateFee(TransactionService transactionService, IConfiguration configuration) : Endpoint<EstimateFeeRequest, EstimateFeeResponse>
{
    private readonly ulong _revenueFee = 
        ulong.TryParse(configuration["File:RevenueFee"], out ulong revenueFee) ? revenueFee : 2_000_000UL;

    public override void Configure()
    {
        Post("/upload/estimate-fee");
        AllowAnonymous();
        Description(x => x
            .WithTags("Upload")
            .WithSummary("Estimate upload fee")
            .WithDescription("Calculate the estimated fee for uploading a file of specified size."));
    }

    public override async Task HandleAsync(EstimateFeeRequest req, CancellationToken ct)
    {
        ulong estimatedFee = transactionService.CalculateFee(req.ContentLength, _revenueFee);
        await SendOkAsync(new EstimateFeeResponse(estimatedFee), ct);
    }
}