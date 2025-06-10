using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using PaylKoyn.Data.Responses;

namespace PaylKoyn.Node.Data;

public record TxStatus(byte[] TxRaw, bool IsSent, bool IsConfirmed)
{
    public byte[] TxRaw { get; set; } = TxRaw;
    public bool IsSent { get; set; } = IsSent;
    public bool IsConfirmed { get; set; } = IsConfirmed;
}

public record Wallet
{
    public int Id { get; set; }
    public string? Address { get; set; } = null;
    public string? UserAddress { get; set; } = null;
    public string? AdaFsId { get; set; } = null;
    public string? AirdropTxHash { get; set; } = null;
    public string? RefundTxHash { get; set; } = null;
    public string? TransactionsRaw { get; set; } = null;
    public int FileSize { get; set; } = 0;
    public UploadStatus Status { get; set; } = UploadStatus.Waiting;
    public UploadStatus? LastValidStatus { get; set; } = null;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public List<TxStatus>? Transactions
    {
        get => TransactionsRaw is null ? null : JsonSerializer.Deserialize<List<TxStatus>>(TransactionsRaw);
        set => TransactionsRaw = value is null ? null : JsonSerializer.Serialize(value);
    }
}