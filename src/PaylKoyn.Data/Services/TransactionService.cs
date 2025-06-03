using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Network.Cbor.LocalStateQuery;
using Chrysalis.Tx.Builders;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;
using Chrysalis.Tx.Utils;
using Chrysalis.Wallet.Utils;
using PaylKoyn.Data.Models.Template;
using Address = Chrysalis.Wallet.Models.Addresses.Address;
using CborAddress = Chrysalis.Cbor.Types.Cardano.Core.Common.Address;

namespace PaylKoyn.Data.Services;


public class TransactionService()
{
    public static TransactionTemplate<TransferParams> Transfer(ICardanoDataProvider provider)
    {
        TransactionTemplate<TransferParams> transferTemplate = TransactionTemplateBuilder<TransferParams>.Create(provider)
            .AddOutput((options, parameters) =>
            {
                options.To = "to";
                options.Amount = new Lovelace(parameters.Amount);
            })
            .Build();

        return transferTemplate;
    }

    public List<Transaction> UploadFile(
        string address,
        byte[] file,
        string fileName,
        string contentType,
        List<ResolvedInput> inputs,
        ProtocolParams protocolParams,
        string revenueAddress
    )
    {
        int _maxTxSize = (int)(protocolParams.MaxTransactionSize ?? 16384) - 300;
        Transaction initialTx = UploadFileTxBuilder(address, file, fileName, contentType, "", inputs, protocolParams, true, HashUtil.Blake2b256(file));
        byte[] initialTxCborBytes = CborSerializer.Serialize(initialTx);
        int initialTxSize = initialTxCborBytes.Length;

        if (initialTxSize <= _maxTxSize)
        {
            return [initialTx];
        }

        return BuildTransactions(address, file, fileName, contentType, inputs, protocolParams, revenueAddress, _maxTxSize);

    }

    public ulong CalculateFee(
        int fileSize,
        ulong revenueFee,
        ulong maxTxSize = 16384
    )
    {
        decimal splitCount = Math.Ceiling((decimal)fileSize / maxTxSize);

        return (ulong)(splitCount * 900000 + revenueFee);
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

    private static List<Transaction> BuildTransactions(
       string address,
       byte[] file,
       string fileName,
       string contentType,
       List<ResolvedInput> inputs,
       ProtocolParams protocolParams,
       string revenueAddress,
       int maxTxSize
   )
    {
        List<byte[]> fileChunks = [];
        byte[] currentBuffer = [];
        int filePosition = 0;

        const int baseChunkSize = 64;

        while (filePosition < file.Length)
        {
            bool keepAdding = true;

            while (keepAdding && filePosition < file.Length)
            {
                int remainingBytes = file.Length - filePosition;
                int chunkSize = Math.Min(baseChunkSize, remainingBytes);

                byte[] newChunk = new byte[chunkSize];
                Buffer.BlockCopy(file, filePosition, newChunk, 0, chunkSize);

                byte[] testBuffer = new byte[currentBuffer.Length + newChunk.Length];
                Buffer.BlockCopy(currentBuffer, 0, testBuffer, 0, currentBuffer.Length);
                Buffer.BlockCopy(newChunk, 0, testBuffer, currentBuffer.Length, newChunk.Length);

                Transaction testTx = UploadFileTxBuilder(
                    address,
                    testBuffer,
                    fileName,
                    contentType,
                    "",
                    inputs,
                    protocolParams,
                    false,
                    null
                );

                byte[] testTxCborBytes = CborSerializer.Serialize(testTx);
                int testTxSize = testTxCborBytes.Length;

                if (testTxSize > maxTxSize)
                {
                    if (currentBuffer.Length == 0)
                    {
                        currentBuffer = newChunk;
                        filePosition += chunkSize;
                    }
                    keepAdding = false;
                }
                else
                {
                    currentBuffer = testBuffer;
                    filePosition += chunkSize;
                }
            }

            if (currentBuffer.Length > 0)
            {
                fileChunks.Add(currentBuffer);
                currentBuffer = [];
            }
        }

        fileChunks.Reverse();

        List<Transaction> transactions = [];
        string next = "";

        int index = 0;
        foreach (byte[] chunk in fileChunks)
        {
            bool isLastChunk = index == fileChunks.Count - 1;

            PostMaryTransaction chunkTx = (PostMaryTransaction)UploadFileTxBuilder(
                isLastChunk ? revenueAddress : address,
                chunk,
                fileName,
                contentType,
                next,
                inputs,
                protocolParams,
                isLastChunk,
                isLastChunk ? HashUtil.Blake2b256(file) : null
            );

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