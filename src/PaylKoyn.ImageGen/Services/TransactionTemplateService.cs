using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Types.Cardano.Core;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Tx.Builders;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Providers;
using Chrysalis.Tx.Utils;
using Chrysalis.Wallet.Models.Enums;
using PaylKoyn.ImageGen.Utils;
using WalletAddress = Chrysalis.Wallet.Models.Addresses.Address;

namespace PaylKoyn.ImageGen.Services;


public record MintNftParams(
    string ChangeAddress,
    string UserAddress,
    string RewardAddress,
    string MintingAddress,
    ulong InvalidAfter,
    Metadata Metadata,
    Dictionary<string, int> MintAssets
) : ITransactionParameters
{
    public Dictionary<string, (string address, bool isChange)> Parties { get; set; } = new()
    {
        { "change", (ChangeAddress, true) },
        { "user", (UserAddress, false) },
        { "reward", (RewardAddress, false) },
        { "minting", (MintingAddress, false) }
    };
};

public class TransactionTemplateService(IConfiguration configuration, ICardanoDataProvider provider)
{
    private readonly NetworkType _networkType = configuration.GetValue<int>("CardanoNodeConnection:NetworkMagic", 2) switch
    {
        764824073 => NetworkType.Mainnet,
        2 => NetworkType.Preview,
        _ => NetworkType.Preprod
    };

    public TransactionTemplate<MintNftParams> MintNftTemplate()
    {
        TransactionTemplateBuilder<MintNftParams> builder = TransactionTemplateBuilder<MintNftParams>.Create(provider);

        builder.AddOutput((options, parameters) =>
            {
                NativeScript mintingScript = ScriptUtil.GetMintingScript(parameters.MintingAddress, parameters.InvalidAfter);
                string mintingPolicyId = ScriptUtil.GetPolicyId(mintingScript);
                byte[] mintingPolicyIdBytes = Convert.FromHexString(mintingPolicyId);
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
            NativeScript mintingScript = ScriptUtil.GetMintingScript(parameters.MintingAddress, parameters.InvalidAfter);
            string mintingPolicyId = ScriptUtil.GetPolicyId(mintingScript);
            options.Policy = mintingPolicyId;
            options.Assets = parameters.MintAssets;
        });

        builder.AddNativeScript((parameters) =>
        {
            NativeScript _mintingScript = ScriptUtil.GetMintingScript(parameters.MintingAddress, parameters.InvalidAfter);
            return _mintingScript;
        });

        builder.AddMetadata((parameters) =>
        {
            return parameters.Metadata;
        });

        SlotNetworkConfig slotNetworkConfig = SlotUtil.GetSlotNetworkConfig(_networkType);
        long currentSlot = SlotUtil.GetSlotFromUTCTime(slotNetworkConfig, DateTime.UtcNow) - 100;
        long invalidHereAfterSlot = currentSlot + 1000;

        builder.AddRequiredSigner("minting");
        builder.SetValidFrom((ulong)currentSlot);
        builder.SetValidTo((ulong)invalidHereAfterSlot);

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
            outputs.RemoveAt(outputs.Count - 1);
            outputs.Add(output);

            txBuilder.SetOutputs(outputs);
            txBuilder.changeOutput = output;
        });

        return builder.Build();
    }
}