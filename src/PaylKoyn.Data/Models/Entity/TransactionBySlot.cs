using Argus.Sync.Data.Models;

namespace PaylKoyn.Data.Models.Entity;

public record TransactionBySlot(
    string Hash,
    ulong Slot,
    byte[] TxMetadatumRaw,
    byte[] TxRaw
) : IReducerModel;