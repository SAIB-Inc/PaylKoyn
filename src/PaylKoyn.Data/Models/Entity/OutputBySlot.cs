using Argus.Sync.Data.Models;

namespace PaylKoyn.Data.Models.Entity;

public record OutputBySlot(
    string OutRef,
    ulong Slot,
    string SpentTxHash,
    string Address,
    byte[] OutputRaw
) : IReducerModel;