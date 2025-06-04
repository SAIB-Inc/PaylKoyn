using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Wallet.Utils;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Models.Api.Response.Data;
using PaylKoyn.Data.Models.Entity;

namespace PaylKoyn.API.Endpoints.Transaction;

public record SubmitTransactionRequest(byte[] TransactionCbor);

public class SubmitTransactionBinder : IRequestBinder<SubmitTransactionRequest>
{
    public async ValueTask<SubmitTransactionRequest> BindAsync(BinderContext ctx, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await ctx.HttpContext.Request.Body.CopyToAsync(ms, ct);
        
        return new SubmitTransactionRequest(ms.ToArray());
    }
}
public class SubmitTransaction(IDbContextFactory<PaylKoynDbContext> dbContextFactory) : Endpoint<SubmitTransactionRequest>
{
    public override void Configure()
    {
        Post("/tx/submit");
        AllowAnonymous();

        RequestBinder(new SubmitTransactionBinder());

        Description(d => d
            .WithTags("Transactions")
            .Accepts<byte[]>("application/cbor")
            .Produces<ScriptResponse[]>(StatusCodes.Status200OK)
            .ProducesProblemFE(StatusCodes.Status400BadRequest)
            .ProducesProblemFE(StatusCodes.Status500InternalServerError)
            .WithName("SubmitTransaction")
        );
    }

    public override async Task HandleAsync(SubmitTransactionRequest req, CancellationToken ct)
    {
        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        PostMaryTransaction tx = CborSerializer.Deserialize<PostMaryTransaction>(req.TransactionCbor);
        string txHash = Convert.ToHexStringLower(HashUtil.Blake2b256(CborSerializer.Serialize(tx.TransactionBody)));

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        TransactionSubmissions submission = new(txHash, req.TransactionCbor, TransactionStatus.Pending, now, null);

        dbContext.TransactionSubmissions.Add(submission);
        await dbContext.SaveChangesAsync(ct);

        await SendAsync(txHash, cancellation: ct);
    }
}
