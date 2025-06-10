namespace PaylKoyn.Data.Responses;

public enum UploadStatus
{
    Waiting,
    Paid,
    Queued,
    Uploaded,
    Airdropped,
    RefundRequested,
    Refunded,
    Failed
}

public record UploadDetailsResponse(
    string Address,
    string? AdaFsId,
    string? RefundTxHash,
    int FileSize,
    string Status,
    DateTime LastUpdated
);