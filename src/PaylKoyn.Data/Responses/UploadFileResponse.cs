namespace PaylKoyn.Data.Responses;

public record UploadFileResponse(string Message, decimal FileSize, decimal EstimatedFee);