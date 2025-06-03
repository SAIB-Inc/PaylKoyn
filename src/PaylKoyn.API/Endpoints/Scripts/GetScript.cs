using Chrysalis.Cbor.Extensions;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Tx.Extensions;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Models.Api.Response.Data;

namespace PaylKoyn.API.Endpoints.Scripts;

public class GetScript(IDbContextFactory<PaylKoynDbContext> dbContextFactory) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/scripts/{scriptHash}");
        AllowAnonymous();

        Description(d => d
            .WithTags("Scripts")
            .Produces<ScriptResponse[]>(StatusCodes.Status200OK)
            .ProducesProblemFE(StatusCodes.Status400BadRequest)
            .ProducesProblemFE(StatusCodes.Status500InternalServerError)
            .WithName("GetScript")
        );
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string scriptHash = Route<string>("scriptHash", isRequired: true)!;

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

        string type = script switch
        {
            PlutusV1Script => "plutusV1",
            PlutusV2Script => "plutusV2",
            PlutusV3Script => "plutusV3",
            _ => "Unknown"
        };

        ScriptResponse response = new(scriptHash, type, (ulong)script.Bytes().Length);

        await SendOkAsync(response, cancellation: ct);
    }
}
