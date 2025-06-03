namespace PaylKoyn.Data.Responses;

public record UploadFileResponse(string Message, string AdaFsId, long FileSize);