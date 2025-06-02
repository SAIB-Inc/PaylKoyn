using Argus.Sync.Data.Models;

namespace PaylKoyn.Data.Models.Entity;

public record OutputBySlot(
    string OutRef,
    ulong Slot,
    string SpentTxHash,
    ulong? SpentSlot,
    string Address,
    string BlockHash,
    string? ScriptDataHash,
    byte[] Raw
) : IReducerModel;