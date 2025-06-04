namespace PaylKoyn.Data.Models.Entity;

public record TransactionSubmissions(
    string Hash,
    byte[] TxRaw,
    TransactionStatus Status,
    long DateSubmitted,
    ulong? ConfirmedSlot
);