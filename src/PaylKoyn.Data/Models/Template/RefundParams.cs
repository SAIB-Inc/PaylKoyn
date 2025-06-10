using Chrysalis.Tx.Models;

namespace PaylKoyn.Data.Models.Template;

public record RefundParams(string From, string To, ulong Amount) : ITransactionParameters
{
    public Dictionary<string, (string address, bool isChange)> Parties { get; set; } = new()
    {
        { "change", (From, false) },
        { "to", (To, false) },
    };
}