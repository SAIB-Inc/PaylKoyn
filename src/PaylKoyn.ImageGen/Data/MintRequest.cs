namespace PaylKoyn.ImageGen.Data;

public enum MintStatus
{
    Waiting,
    Paid,
    Processing,
    Uploading,
    Uploaded,
    NftSent,
    TokenSent,
    RefundRequested,
    Refunded,
    Failed
}

public record MintRequest(
    string? Address,
    int WalletIndex,
    string UserAddress,
    string? UploadPaymentAddress,
    ulong UploadPaymentAmount,
    string? PolicyId,
    string? AssetName,
    string? NftMetadata,
    string? AdaFsId,
    string? MintTxHash,
    string? AirdropTxHash,
    string? Traits,
    byte[]? Image,
    MintStatus Status,
    int? NftNumber,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public int Id { get; init; }
    public string? Address { get; set; } = Address;
    public int WalletIndex { get; init; } = WalletIndex;
    public string UserAddress { get; init; } = UserAddress;
    public string? UploadPaymentAddress { get; set; } = UploadPaymentAddress;
    public ulong UploadPaymentAmount { get; set; } = UploadPaymentAmount;
    public string? PolicyId { get; set; } = PolicyId;
    public string? AssetName { get; set; } = AssetName;
    public string? NftMetadata { get; set; } = NftMetadata;
    public string? AdaFsId { get; set; } = AdaFsId;
    public string? MintTxHash { get; set; } = MintTxHash;
    public string? AirdropTxHash { get; set; } = AirdropTxHash;
    public string? RefundTxHash { get; set; } = null;
    public string? Traits { get; set; } = Traits;
    public byte[]? Image { get; set; } = Image;
    public MintStatus Status { get; set; } = Status;
    public MintStatus? LastValidStatus { get; set; } = null;
    public int? NftNumber { get; set; } = NftNumber;
    public DateTime CreatedAt { get; init; } = CreatedAt;
    public DateTime UpdatedAt { get; set; } = UpdatedAt;
}