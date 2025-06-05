using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace PaylKoyn.Data.Models;

public enum UploadStatus
{
    Waiting,
    Paid,
    Queued,
    Uploaded,
    RefundRequested,
    Refunded,
    Failed
}

public record TxStatus(byte[] TxRaw, bool IsSent, bool IsConfirmed)
{
    public byte[] TxRaw { get; set; } = TxRaw;
    public bool IsSent { get; set; } = IsSent;
    public bool IsConfirmed { get; set; } = IsConfirmed;
}

public record Wallet(string Address, int Index, string? AdaFsId = null) // ← Remove Transactions from constructor
{
    public string Address { get; init; } = Address;
    public int Index { get; init; } = Index;
    public string? AdaFsId { get; set; } = AdaFsId;
    public string? TransactionsRaw { get; set; } = null;
    public int FileSize { get; set; } = 0;
    public UploadStatus Status { get; set; } = UploadStatus.Waiting;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public List<TxStatus>? Transactions
    {
        get => TransactionsRaw is null ? null : JsonSerializer.Deserialize<List<TxStatus>>(TransactionsRaw);
        set => TransactionsRaw = value is null ? null : JsonSerializer.Serialize(value);
    }
}