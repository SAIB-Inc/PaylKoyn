namespace PaylKoyn.Data.Models.Api.Response.Data;

public record ScriptResponse(
    string ScriptHash,
    string Type,
    ulong SerialisedSize
);
