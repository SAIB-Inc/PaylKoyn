using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Wallet.Utils;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;

namespace PaylKoyn.ImageGen.Utils;

public static class ScriptUtil
{
    public static NativeScript GetMintingScript(string mintingAddress, ulong invalidAfterSlot)
    {
        WalletAddress address = new(mintingAddress);
        ScriptPubKey scriptPubKey = new(0, address.GetPaymentKeyHash()!);
        InvalidHereafter invalidHereafter = new(5, invalidAfterSlot);
        NativeScript nativeScript = new ScriptAll(1, new([scriptPubKey, invalidHereafter]));
        return nativeScript;
    }

    public static string GetPolicyId(NativeScript script)
    {
        return Convert.ToHexString(HashUtil.Blake2b224([0, .. CborSerializer.Serialize(script)])).ToLowerInvariant();
    }
}