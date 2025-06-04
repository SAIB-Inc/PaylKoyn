using Chrysalis.Cbor.Types.Cardano.Core.Common;

namespace PaylKoyn.Data.Utils;

public static class AssetUtils
{
    public static LovelaceWithMultiAsset ConvertToLovelaceWithMultiAsset(Dictionary<string, Dictionary<string, ulong>> assets)
    {
        ulong lovelaceValue = 0;
        if (assets.TryGetValue("", out Dictionary<string, ulong>? policyValue) && policyValue.TryGetValue("", out ulong assetValue))
        {
            lovelaceValue = assetValue;
        }

        Lovelace lovelace = new(lovelaceValue);

        Dictionary<byte[], TokenBundleOutput> multiAssetDict = [];

        foreach (KeyValuePair<string, Dictionary<string, ulong>> policyGroup in assets)
        {
            string policyId = policyGroup.Key;

            if (string.IsNullOrEmpty(policyId)) continue;

            byte[] policyIdBytes = Convert.FromHexString(policyId);

            Dictionary<byte[], ulong> tokenBundle = [];

            foreach (KeyValuePair<string, ulong> asset in policyGroup.Value)
            {
                string assetName = asset.Key;
                ulong amount = asset.Value;
                byte[] assetNameBytes = Convert.FromHexString(assetName);
                tokenBundle[assetNameBytes] = amount;
            }

            if (tokenBundle.Count > 0)
            {
                multiAssetDict[policyIdBytes] = new TokenBundleOutput(tokenBundle);
            }
        }

        MultiAssetOutput multiAsset = new(multiAssetDict);

        return new LovelaceWithMultiAsset(lovelace, multiAsset);
    }
}
