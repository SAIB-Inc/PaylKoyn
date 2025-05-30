using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Network.Cbor.LocalStateQuery;
using Chrysalis.Tx.Builders;
using Chrysalis.Tx.Models.Cbor;
using Chrysalis.Tx.Utils;
using Chrysalis.Wallet.Utils;
using Address = Chrysalis.Wallet.Models.Addresses.Address;
using CborAddress = Chrysalis.Cbor.Types.Cardano.Core.Common.Address;

namespace PaylKoyn.Data.Services;


public class TransactionService()
{
    public List<Transaction> UploadFile(
        string address,
        byte[] file,
        string fileName,
        string contentType,
        List<ResolvedInput> inputs,
        ProtocolParams protocolParams
    )
    {
        int _maxTxSize = (int)(protocolParams.MaxTransactionSize ?? 16384) - 107;
        Transaction initialTx = UploadFileTxBuilder(address, file, fileName, contentType, "", inputs, protocolParams, true, HashUtil.Blake2b256(file));
        byte[] initialTxCborBytes = CborSerializer.Serialize(initialTx);
        int initialTxSize = initialTxCborBytes.Length;

        if (initialTxSize <= _maxTxSize)
        {
            return [initialTx];
        }

        int splitCount = (int)Math.Ceiling((double)initialTxSize / _maxTxSize);

        List<byte[]> fileChunks = SplitFile(file, splitCount);
        fileChunks.Reverse();

        List<Transaction> transactions = [];
        string next = "";

        int index = 0;
        foreach (byte[] chunk in fileChunks)
        {
            bool isLastChunk = index == fileChunks.Count - 1;
            
            PostMaryTransaction chunkTx = (PostMaryTransaction)UploadFileTxBuilder(
                address,
                chunk,
                fileName,
                contentType,
                next,
                inputs,
                protocolParams,
                isLastChunk,
                isLastChunk ? HashUtil.Blake2b256(file) : null
            );

            byte[] chunkTxCborBytes = CborSerializer.Serialize(chunkTx);
            byte[] chunkTxBodyBytes = CborSerializer.Serialize(chunkTx.TransactionBody);
            byte[] chunkTxHash = HashUtil.Blake2b256(chunkTxBodyBytes);
            next = Convert.ToHexString(chunkTxHash);

            TransactionOutput chunkOutput = chunkTx.TransactionBody.Outputs().FirstOrDefault()!;
            inputs = [new ResolvedInput(new TransactionInput(chunkTxHash, 0), chunkOutput)];
            transactions.Add(chunkTx);
            index++;
        }

        return transactions;
    }

    private static Transaction UploadFileTxBuilder(
        string address,
        byte[] file,
        string fileName,
        string contentType,
        string next,
        List<ResolvedInput> inputs,
        ProtocolParams protocolParams,
        bool isLastChunk,
        byte[]? checksum = null
    )
    {
        Address walletAddress = new(address);
        TransactionBuilder txBuilder = TransactionBuilder.Create(protocolParams);

        ulong lovelaceConsumed = 0;
        foreach (ResolvedInput input in inputs)
        {
            txBuilder.AddInput(input.Outref);
            lovelaceConsumed += input.Output.Amount().Lovelace();
        }

        AlonzoTransactionOutput output = new(
            new CborAddress(walletAddress.ToBytes()),
            new Lovelace(lovelaceConsumed),
            null
        );

        int fileSize = file.Length;
        int splitCount = (int)Math.Ceiling((double)fileSize / 64);

        List<byte[]> fileChunks = SplitFile(file, splitCount);
        List<TransactionMetadatum> fileChunksByteArray = [.. fileChunks.Select(chunk => new MetadatumBytes(chunk))];

        Dictionary<TransactionMetadatum, TransactionMetadatum> metadata = [];

        if (isLastChunk)
        {
            metadata.Add(new MetadataText("version"), new MetadatumIntLong(1));
            metadata.Add(new MetadataText("metadata"), new MetadatumMap(new Dictionary<TransactionMetadatum, TransactionMetadatum>
                {
                    { new MetadataText("filename"), new MetadataText(fileName) },
                    { new MetadataText("contentType"), new MetadataText(contentType) },
                }));
        }

        metadata.Add(new MetadataText("payload"), new MetadatumList(fileChunksByteArray));
        metadata.Add(new MetadataText("next"), new MetadatumBytes(Convert.FromHexString(next)));

        if (isLastChunk)
        {
            metadata.Add(new MetadataText("checksum"), new MetadatumBytes(checksum!));
        }

        Metadata labeledMetadata = new(
            new Dictionary<ulong, TransactionMetadatum>
            {
                { 6673, new MetadatumMap(metadata) }
            });

        
        PostAlonzoAuxiliaryDataMap auxData = new(labeledMetadata, null, null, null, null);
        txBuilder.SetAuxiliaryData(auxData);
        byte[] auxDataCborBytes = CborSerializer.Serialize(auxData);
        txBuilder.SetAuxiliaryDataHash(HashUtil.Blake2b256(auxDataCborBytes));

        txBuilder.SetFee(20000000UL);

        Transaction draftTx = txBuilder.Build();
        var draftTxCborBytes = CborSerializer.Serialize(draftTx);
        ulong draftTxCborLength = (ulong)draftTxCborBytes.Length;

        var fee = FeeUtil.CalculateFeeWithWitness(draftTxCborLength, protocolParams!.MinFeeA!.Value, protocolParams.MinFeeB!.Value, 2);

        txBuilder.SetFee(fee);

        Lovelace outputValueWithFee = new(lovelaceConsumed - fee);

        output = output with
        {
            Amount = outputValueWithFee
        };

        txBuilder.AddOutput(output);

        Transaction transaction = txBuilder.Build();

        return transaction;
    }

    private static List<byte[]> SplitFile(byte[] file, int splitCount)
    {
        List<byte[]> result = new(splitCount);

        for (int i = 0; i < splitCount; i++)
        {
            int startIndex = (int)((long)file.Length * i / splitCount);
            int endIndex = (int)((long)file.Length * (i + 1) / splitCount);
            int length = endIndex - startIndex;

            byte[] part = new byte[length];
            if (length > 0)
            {
                Array.Copy(file, startIndex, part, 0, length);
            }
            result.Add(part);
        }

        return result;

    }

}