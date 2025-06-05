using Chrysalis.Tx.Models;

namespace PaylKoyn.Data.Models.Template;

public record MultiAssetTransferParams(string From, string To, Dictionary<string, Dictionary<string, ulong>> Assets) : ITransactionParameters
{
    public Dictionary<string, (string address, bool isChange)> Parties { get; set; } = new()
    {
        { "change", (From, true) },
        { "to", (To, false) },
    };
}