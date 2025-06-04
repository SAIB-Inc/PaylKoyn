namespace PaylKoyn.Data.Models.Entity;

public record TransactionSubmission(
    string Hash,
    byte[] TxRaw,
    TransactionStatus Status,
    long DateSubmitted,
    ulong? ConfirmedSlot
);