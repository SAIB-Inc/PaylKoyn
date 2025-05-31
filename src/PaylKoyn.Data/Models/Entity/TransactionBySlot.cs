using Argus.Sync.Data.Models;

namespace PaylKoyn.Data.Models.Entity;

public record TransactionBySlot(
    string Hash,
    ulong Slot,
    byte[] Metadata,
    byte[] Body
) : IReducerModel;