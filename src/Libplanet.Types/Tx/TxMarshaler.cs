using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bencodex.Json;
using Bencodex.Types;
using Libplanet.Serialization;

namespace Libplanet.Types.Tx;

public static class TxMarshaler
{
    private static readonly Codec Codec = new();

    private static readonly BencodexJsonConverter BencodexJsonConverter = new();
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            BencodexJsonConverter,
        },
    };

    public static IValue MarshalTxInvoice(this TxInvoice invoice) => ModelSerializer.Serialize(invoice);

    public static IValue MarshalTxSigningMetadata(this TxSigningMetadata metadata)
        => ModelSerializer.Serialize(metadata);

    public static IValue MarshalUnsignedTx(this UnsignedTx unsignedTx)
        => ModelSerializer.Serialize(unsignedTx);

    public static string SerializeUnsignedTx(this UnsignedTx unsignedTx)
    {
        var dict = unsignedTx.MarshalUnsignedTx();
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });
        BencodexJsonConverter.Write(writer, dict, SerializerOptions);
        ms.Position = 0;
        using var sr = new StreamReader(ms);
        return sr.ReadToEnd();
    }

    public static IValue MarshalTransaction(this Transaction transaction)
        => ModelSerializer.Serialize(transaction);

    public static IValue MarshalTransaction(UnsignedTx unsignedTx, ImmutableArray<byte> signature)
        => ModelSerializer.Serialize(Transaction.Create(unsignedTx, signature));

    public static TxId GetTxId(UnsignedTx unsignedTx, ImmutableArray<byte> signature)
    {
        var value = MarshalTransaction(unsignedTx, signature);
        var payload = Codec.Encode(value);
        return new TxId(SHA256.HashData(payload));
    }

    public static TxInvoice UnmarshalTxInvoice(IValue dictionary)
        => ModelSerializer.Deserialize<TxInvoice>(dictionary);

    public static TxSigningMetadata UnmarshalTxSigningMetadata(IValue dictionary)
        => ModelSerializer.Deserialize<TxSigningMetadata>(dictionary);

    public static UnsignedTx UnmarshalUnsignedTx(IValue dictionary)
        => ModelSerializer.Deserialize<UnsignedTx>(dictionary);

    // public static ImmutableArray<byte>? UnmarshalTransactionSignature(
    //     Bencodex.Types.Dictionary dictionary
    // ) =>
    //     dictionary.TryGetValue(SignatureKey, out IValue v) && v is Binary bin
    //         ? bin.ToImmutableArray()
    //         : (ImmutableArray<byte>?)null;

    public static Transaction UnmarshalTransaction(IValue dictionary)
        => ModelSerializer.Deserialize<Transaction>(dictionary);

    public static UnsignedTx DeserializeUnsignedTx(byte[] bytes)
        => ModelSerializer.DeserializeFromBytes<UnsignedTx>(bytes);

    public static UnsignedTx DeserializeUnsignedTx(string json)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var reader = new Utf8JsonReader(ms.ToArray());
        var value = BencodexJsonConverter.Read(ref reader, typeof(IValue), SerializerOptions)
            ?? throw new InvalidOperationException("Failed to parse the unsigned transaction.");
        if (value is Dictionary dict)
        {
            return UnmarshalUnsignedTx(dict);
        }

        throw new DecodingException(
            $"Expected a {typeof(Bencodex.Types.Dictionary).FullName}, " +
            $"but {value.GetType().Name} given."
        );
    }

    public static Transaction UnmarshalTransactionWithoutVerification(IValue dictionary)
        => ModelSerializer.Deserialize<Transaction>(dictionary);

    public static Transaction DeserializeTransactionWithoutVerification(byte[] bytes)
        => ModelSerializer.DeserializeFromBytes<Transaction>(bytes);

    internal static Transaction DeserializeTransactionWithoutVerification(
        ImmutableArray<byte> bytes)
    {
        byte[] arrayView = Unsafe.As<ImmutableArray<byte>, byte[]>(ref bytes);
        return DeserializeTransactionWithoutVerification(arrayView);
    }
}
