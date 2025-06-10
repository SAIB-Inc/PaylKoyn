namespace PaylKoyn.ImageGen.Data.Responses;

public record MintRequestDetailsResponse(
    string Id,
    string Status,
    string UploadAddress,
    string? AdaFsId,
    string? MintTxHash,
    string? AirdropTxHash,
    string? RefundTxHash,
    decimal FileSize,
    DateTime UpdatedAt
);
