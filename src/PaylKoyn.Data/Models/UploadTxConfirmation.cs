using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace PaylKoyn.Data.Models;

public record TxConfirmation(string TxHash, bool IsConfirmed);

public record UploadTxConfirmation(string TxHash, bool IsConfirmed)
{
    public string Address { get; init; } = string.Empty;
    public string TxConfirmationsJson { get; set; } = string.Empty;

    [NotMapped]
    public List<TxConfirmation> TxConfirmations
    {
        get => string.IsNullOrEmpty(TxConfirmationsJson) ? [] : JsonSerializer.Deserialize<List<TxConfirmation>>(TxConfirmationsJson) ?? [];
        set => TxConfirmationsJson = JsonSerializer.Serialize(value);
    }
}