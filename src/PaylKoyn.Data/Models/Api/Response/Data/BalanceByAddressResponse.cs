namespace PaylKoyn.Data.Models.Api.Response.Data;

public record BalanceByAddressResponse(
    ulong Lovelace,
    Dictionary<string, Dictionary<string, ulong>> MultiAsset
);

public record UnspentOutput(
    string Address,
    string TxHash,
    ulong Index,
    IEnumerable<Amount> Amount,
    string Block,
    string? DataHash,
    string? InlineDatum,
    string? ReferenceScriptHash
);

public record Amount(
    string Unit,
    ulong Quantity
);