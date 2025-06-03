using Chrysalis.Tx.Models;

namespace PaylKoyn.Data.Models.Template;

public record TransferParams(string From, string To, ulong Amount) : ITransactionParameters
{
    public Dictionary<string, (string address, bool isChange)> Parties { get; set; } = new()
    {
        { "change", (From, true) },
        { "to", (To, false) },
    };
}