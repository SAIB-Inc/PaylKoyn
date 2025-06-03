namespace PaylKoyn.Data.Models.Api.Response.Data;

public record GetUtxoByAddressResponse(
    string Address,
    string TxHash,
    ulong TxIndex,
    ulong OutputIndex,
    IEnumerable<Amount> Amount,
    string Block,
    string? DataHash,
    string? InlineDatum,
    string? ReferenceScriptHash
);

public record Amount(
    string Unit,
    string Quantity
);