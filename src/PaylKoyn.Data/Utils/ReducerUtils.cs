using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Wallet.Models.Addresses;

namespace PaylKoyn.Data.Utils;

public static class ReducerUtils
{
    public static bool IsRelevantAddress(TransactionOutput output, out string bech32Address)
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
}