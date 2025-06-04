namespace PaylKoyn.ImageGen.Data.Responses;

public record MintRequestDetailsResponse(
    string Id,
    string Status,
    string UploadAddress,
    string? AdaFsId,
    string? MintTxHash,
    decimal FileSize,
    DateTime UpdatedAt
);
