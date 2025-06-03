namespace PaylKoyn.ImageGen.Data;

public enum MintStatus
{
    Pending,
    PaymentReceived,
    MetadataGenerated,
    UploadPaymentSent,
    ImageUploaded,
    Minted,
    Failed
}

public record MintRequest(
    string Id,
    int WalletIndex,
    string UserAddress,
    string? UploadPaymentAddress,
    ulong UploadPaymentAmount,
    string? PolicyId,
    string? AssetName,
    string? NftMetadata,
    string? AdaFsId,
    string? MintTxHash,
    string? Traits,
    byte[]? Image,
    MintStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public string Id { get; init; } = Id;
    public int WalletIndex { get; init; } = WalletIndex;
    public string UserAddress { get; init; } = UserAddress;
    public string? UploadPaymentAddress { get; set; } = UploadPaymentAddress;
    public ulong UploadPaymentAmount { get; set; } = UploadPaymentAmount;
    public string? PolicyId { get; set; } = PolicyId;
    public string? AssetName { get; set; } = AssetName;
    public string? NftMetadata { get; set; } = NftMetadata;
    public string? AdaFsId { get; set; } = AdaFsId;
    public string? MintTxHash { get; set; } = MintTxHash;
    public string? Traits { get; set; } = Traits;
    public byte[]? Image { get; set; } = Image;
    public MintStatus Status { get; set; } = Status;
    public DateTime CreatedAt { get; init; } = CreatedAt;
    public DateTime UpdatedAt { get; set; } = UpdatedAt;
}