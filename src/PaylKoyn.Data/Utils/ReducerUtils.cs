using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Wallet.Utils;
using Address = Chrysalis.Wallet.Models.Addresses.Address;

namespace PaylKoyn.Data.Utils;

public static class ReducerUtils
{
    public static bool TryGetBech32Address(in TransactionOutput output, out string bech32Address)
    {
        bech32Address = string.Empty;

        try
        {
            Address address = new(output.Address());
            if (!address.ToBech32().StartsWith("addr")) return false;

            bech32Address = address.ToBech32();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryGetScripHash(in TransactionOutput output, out string scriptHash)
    {
        scriptHash = string.Empty;

        try
        {
            if (output.ScriptRef() is null) return false;
            Script script = CborSerializer.Deserialize<Script>(output.ScriptRef()!);

            byte[] scriptBytes = [];

            switch (script)
            {
                case MultiSigScript multiSigScript:
                    byte[] nativeScriptBytes = CborSerializer.Serialize(multiSigScript.Script);
                    scriptBytes = [0, .. nativeScriptBytes];
                    break;
                case PlutusV1Script v1Script:
                    scriptBytes = [1, .. v1Script.ScriptBytes];
                    break;
                case PlutusV2Script v2Script:
                    scriptBytes = [2, .. v2Script.ScriptBytes];
                    break;
                case PlutusV3Script v3Script:
                    scriptBytes = [3, .. v3Script.ScriptBytes];
                    break;
                default:
                    return false;
            }
            
            scriptHash = Convert.ToHexStringLower(HashUtil.Blake2b224(scriptBytes));
            return true;
        }
        catch
        {
            return false;
        }
    }
}