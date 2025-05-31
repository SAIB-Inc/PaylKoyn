namespace PaylKoyn.Data.Models.Api.Response.Data;

public record BalanceByAddressResponse(
    ulong Lovelace,
    Dictionary<string, Dictionary<string, ulong>> MultiAsset
);