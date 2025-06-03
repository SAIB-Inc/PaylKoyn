using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Tx.Builders;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Providers;
using Chrysalis.Wallet.Utils;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;

namespace Paylkoyn.ImageGen.Services;


public record MintNftParams(string ChangeAddress, string UserAddress, string RewardAddress, Metadata Metadata, Dictionary<string, int> MintAssets) : ITransactionParameters
{
    public Dictionary<string, (string address, bool isChange)> Parties { get; set; } = new()
    {
        { "change", (ChangeAddress, true) },
        { "user", (UserAddress, false) },
        { "reward", (RewardAddress, false) }
    };
};

public class TransactionTemplateService(IConfiguration configuration)
{
    private readonly NativeScript _mintingScript = new ScriptPubKey(0, Convert.FromHexString("5eedab004c4540665dca3d44554ad7a9c9092e69b1b05807881122df"));
    private readonly Blockfrost _provider = new(configuration.GetValue<string>("Blockfrost:ProjectId", "previewBVVptlCv4DAR04h3XADZnrUdNTiJyHaJ"));

    public TransactionTemplate<MintNftParams> MintNftTemplate()
    {
        TransactionTemplateBuilder<MintNftParams> builder = TransactionTemplateBuilder<MintNftParams>.Create(_provider);

        byte[] nativeScriptBytes = CborSerializer.Serialize(_mintingScript);
        byte[] mintingPolicyIdBytes = HashUtil.Blake2b224([0, .. nativeScriptBytes]);
        string mintingPolicyId = Convert.ToHexString(mintingPolicyIdBytes).ToLowerInvariant();

        builder.AddOutput((options, parameters) =>
        {
            byte[] assetNameBytes = Convert.FromHexString(parameters.MintAssets.First().Key);
            Dictionary<byte[], ulong> assetDict = new()
            {
                [assetNameBytes] = 1 // Example asset, replace with actual asset logic
            };
            TokenBundleOutput tokenBundle = new(assetDict);
            Dictionary<byte[], TokenBundleOutput> policyDict = new()
            {
                [mintingPolicyIdBytes] = tokenBundle
            };
            MultiAssetOutput multiAssetOutput = new(policyDict);

            options.To = "user";
            options.Amount = new LovelaceWithMultiAsset(
                new(2_000_000UL),
                multiAssetOutput
            );
        });

        builder.AddMint((options, parameters) =>
        {
            options.Policy = mintingPolicyId;
            options.Assets = parameters.MintAssets;
        });

        builder.AddNativeScript((parameters) =>
        {
            return _mintingScript;
        });

        builder.AddMetadata((parameters) =>
        {
            return parameters.Metadata;
        });

        builder.AddRequiredSigner("change");

        builder.SetPreBuildHook((txBuilder, _, parameters) =>
        {
            PostMaryTransaction tx = txBuilder.Build();
            List<TransactionOutput> outputs = [.. tx.TransactionBody.Outputs()];
            AlonzoTransactionOutput output = (AlonzoTransactionOutput)outputs.Last();
            WalletAddress addr = new(parameters.RewardAddress);
            output = output with
            {
                Address = new(addr.ToBytes())
            };
            WalletAddress addr2 = new(output.Address.Value);
            Console.WriteLine($"Reward address: {addr2.ToBech32()}");
            outputs.RemoveAt(outputs.Count - 1);
            outputs.Add(output);

            txBuilder.SetOutputs(outputs);
            txBuilder.changeOutput = output;
        });

        return builder.Build();
    }
}