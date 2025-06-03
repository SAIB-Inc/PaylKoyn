using Chrysalis.Cbor.Extensions;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Tx.Extensions;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
namespace PaylKoyn.API.Endpoints;

public class GetScriptCbor(IDbContextFactory<PaylKoynDbContext> dbContextFactory) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/scripts/{scriptHash}/cbor");
        AllowAnonymous();

    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? scriptHash = Route<string>("scriptHash", isRequired: true);

        await using PaylKoynDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        byte[] outputRaw = await dbContext.OutputsBySlot
            .AsNoTracking()
            .Where(x => x.ScriptHash == scriptHash)
            .Select(x => x.Raw)
            .FirstOrDefaultAsync(ct) ?? [];

        if (outputRaw.Length == 0)
        {
            AddError("No script found.");
            await SendNotFoundAsync(ct);
            return;
        }

        PostAlonzoTransactionOutput output = CborSerializer.Deserialize<PostAlonzoTransactionOutput>(outputRaw);

        byte[] cborEncodedScriptBytes = output.ScriptRef!.GetValue();
        Script script = CborSerializer.Deserialize<Script>(cborEncodedScriptBytes);

        ScriptCborResponse response = new(Convert.ToHexString(script.Bytes()));

        await SendOkAsync(response, cancellation: ct);
    }

}

public record ScriptCborResponse(
    string Cbor
);

