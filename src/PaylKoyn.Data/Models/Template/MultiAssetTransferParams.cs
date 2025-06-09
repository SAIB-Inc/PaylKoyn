using Chrysalis.Tx.Models;

namespace PaylKoyn.Data.Models.Template;

public record Recipient(string Address, Dictionary<string, Dictionary<string, ulong>> Assets);

public record MultiAssetTransferParams(string From, List<Recipient> Recipients) : ITransactionParameters
{
    public Dictionary<string, (string address, bool isChange)> Parties { get; set; } = 
        Recipients.ToDictionary(r => r.Address, r => (r.Address, false))
        .Append(new KeyValuePair<string, (string, bool)>("change", (From, true)))
        .ToDictionary();
}