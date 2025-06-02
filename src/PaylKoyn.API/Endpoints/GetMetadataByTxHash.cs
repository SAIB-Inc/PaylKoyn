using System.Text;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Models.Common;
using PaylKoyn.Data.Models.Entity;
using PaylKoyn.Data.Utils;
using MetadatumMap = Chrysalis.Cbor.Types.Cardano.Core.Transaction.MetadatumMap;

namespace PaylKoyn.API.Endpoints;

public class GetMetadataByTxHash(IDbContextFactory<PaylKoynDbContext> dbContextFactory) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/tx/{txHash}/metadata");
        AllowAnonymous();
        Description(x => x
            .WithTags("Transaction")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        string? txHash = Route<string>("txHash", isRequired: true);

        TransactionBySlot? tx = await dbContext.TransactionsBySlot
            .AsNoTracking()
            .Where(x => x.Hash == txHash)
            .FirstOrDefaultAsync(ct);

        if (tx is null)
        {
            AddError("No payloads found.");
            await SendNotFoundAsync(ct);
            return;
        }

        TransactionMetadatum? txMetadatum = CborSerializer.Deserialize<TransactionMetadatum>(tx.Metadata);
        MetadatumMap? metadataMap = DataUtils.GetMetadataMapBytes(txMetadatum, "metadata");
        string fileName = DataUtils.GetMetadataValueString(metadataMap?.Value, "filename") ?? "unknown.txt";
        string contentType = DataUtils.GetMetadataValueString(metadataMap?.Value, "contentType") ?? "application/octet-stream";
        PaylKoynMetadata metadata = new(
            Version: DataUtils.GetMetadataValueLong(txMetadatum, "version") ?? 0,
            Payload: DataUtils.GetPayloadFromTransactionMetadatum(txMetadatum)!,
            Metadata: new (
                FileName: fileName,
                ContentType: contentType
            ),
            Next: Convert.ToHexStringLower(DataUtils.GetMetadataValueBytes(txMetadatum, "next") ?? [])
        );

        await SendOkAsync(metadata, cancellation: ct);
    }
}