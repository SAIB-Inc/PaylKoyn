namespace PaylKoyn.Data.Models.Common;

public record PaylKoynMetadata(
    int Version,
    Metadata Metadata,
    string Payload,
    string Next
);

public record Metadata(
    string FileName,
    string ContentType
);