using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bencodex.Json;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Tx;

public static class TxMarshaler
{
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";
    private static readonly Binary UpdatedAddressesKey = new("u"u8.ToArray());
    private static readonly Binary TimestampKey = new("t"u8.ToArray());
    private static readonly Binary GenesisHashKey = new("g"u8.ToArray());
    private static readonly Binary MaxGasPriceKey = new("m"u8.ToArray());
    private static readonly Binary GasLimitKey = new("l"u8.ToArray());
    private static readonly Binary NonceKey = new("n"u8.ToArray());
    private static readonly Binary SignerKey = new("s"u8.ToArray());
    private static readonly Binary SignatureKey = new("S"u8.ToArray());
    private static readonly Binary ActionsKey = new("a"u8.ToArray());
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

    [Pure]
    public static Bencodex.Types.Dictionary MarshalTxInvoice(this TxInvoice invoice)
    {
        Bencodex.Types.List updatedAddresses = new(
            invoice.UpdatedAddresses.Select<Address, IValue>(addr => ModelSerializer.Serialize(addr))
        );
        string timestamp = invoice.Timestamp
            .ToUniversalTime()
            .ToString(TimestampFormat, CultureInfo.InvariantCulture);

        Bencodex.Types.Dictionary dict = Bencodex.Types.Dictionary.Empty;
        dict = dict.Add(ActionsKey, [.. invoice.Actions]);

        dict = dict
            .Add(UpdatedAddressesKey, updatedAddresses)
            .Add(TimestampKey, timestamp);

        if (invoice.GenesisHash is { } genesisHash)
        {
            dict = dict.Add(GenesisHashKey, genesisHash.Bytes);
        }

        if (invoice.MaxGasPrice is { } maxGasPrice)
        {
            dict = dict.Add(MaxGasPriceKey, maxGasPrice.ToBencodex());
        }

        if (invoice.GasLimit is { } gasLimit)
        {
            dict = dict.Add(GasLimitKey, gasLimit);
        }

        return dict;
    }

    [Pure]
    public static Bencodex.Types.Dictionary MarshalTxSigningMetadata(
        this TxSigningMetadata metadata
    ) => Dictionary.Empty
        .Add(NonceKey, metadata.Nonce)
        .Add(SignerKey, ModelSerializer.Serialize(metadata.Signer));

    [Pure]
    public static Dictionary MarshalUnsignedTx(this UnsignedTx unsignedTx)
        => (Dictionary)MarshalTxInvoice(unsignedTx.Invoice)
            .AddRange(MarshalTxSigningMetadata(unsignedTx.SigningMetadata));

    [Pure]
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

    [Pure]
    public static Dictionary MarshalTransaction(this Transaction transaction)
        => MarshalTransaction(transaction.UnsignedTx, transaction.Signature);

    public static Dictionary MarshalTransaction(
        UnsignedTx unsignedTx, ImmutableArray<byte> signature)
        => MarshalUnsignedTx(unsignedTx).Add(SignatureKey, signature);

    public static TxId GetTxId(
        UnsignedTx unsignedTx, ImmutableArray<byte> signature)
    {
        var value = MarshalTransaction(unsignedTx, signature);
        var payload = Codec.Encode(value);
        return new TxId(SHA256.HashData(payload));
    }

    [Pure]
    public static TxInvoice UnmarshalTxInvoice(Bencodex.Types.Dictionary dictionary)
    {
        return new TxInvoice
        {
            Actions = dictionary.TryGetValue(ActionsKey, out IValue av) && av is List list
                ? list.ToImmutableArray()
                : [],
            GenesisHash = dictionary.TryGetValue(GenesisHashKey, out IValue gv)
                ? ModelSerializer.Deserialize<BlockHash>(gv)
                : default,
            UpdatedAddresses = ImmutableSortedSet.Create(
                [.. ((List)dictionary[UpdatedAddressesKey]).Select(ModelSerializer.Deserialize<Address>)]),
            Timestamp = DateTimeOffset.ParseExact(
                (Text)dictionary[TimestampKey],
                TimestampFormat,
                CultureInfo.InvariantCulture).ToUniversalTime(),
            MaxGasPrice = dictionary.TryGetValue(MaxGasPriceKey, out IValue mgpv)
                ? FungibleAssetValue.Create(mgpv)
                : null,
            GasLimit = dictionary.TryGetValue(GasLimitKey, out IValue glv)
                ? (long)(Bencodex.Types.Integer)glv
                : 0L,
        };
    }

    [Pure]
    public static TxSigningMetadata UnmarshalTxSigningMetadata(
        Bencodex.Types.Dictionary dictionary
    ) =>
        new(
            Signer: ModelSerializer.Deserialize<Address>(dictionary[SignerKey]),
            Nonce: (Bencodex.Types.Integer)dictionary[NonceKey]
        );

    [Pure]
    public static UnsignedTx UnmarshalUnsignedTx(Bencodex.Types.Dictionary dictionary) =>
        new(
            Invoice: UnmarshalTxInvoice(dictionary),
            SigningMetadata: UnmarshalTxSigningMetadata(dictionary));

    [Pure]
    public static ImmutableArray<byte>? UnmarshalTransactionSignature(
        Bencodex.Types.Dictionary dictionary
    ) =>
        dictionary.TryGetValue(SignatureKey, out IValue v) && v is Binary bin
            ? bin.ToImmutableArray()
            : (ImmutableArray<byte>?)null;

    [Pure]
    public static Transaction UnmarshalTransaction(Bencodex.Types.Dictionary dictionary)
    {
        ImmutableArray<byte>? sig = UnmarshalTransactionSignature(dictionary);
        if (!(sig is { } signature))
        {
            throw new DecodingException("Transaction signature is missing.");
        }

        return UnmarshalUnsignedTx(dictionary).Verify(signature);
    }

    [Pure]
    public static UnsignedTx DeserializeUnsignedTx(byte[] bytes)
    {
        IValue node = Codec.Decode(bytes);
        if (node is Bencodex.Types.Dictionary dict)
        {
            return UnmarshalUnsignedTx(dict);
        }

        throw new DecodingException(
            $"Expected a {typeof(Bencodex.Types.Dictionary).FullName}, " +
            $"but {node.GetType().Name} given.");
    }

    [Pure]
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

    [Pure]
    public static Transaction UnmarshalTransactionWithoutVerification(
        Bencodex.Types.Dictionary dictionary)
    {
        ImmutableArray<byte> sig
            = dictionary.TryGetValue(SignatureKey, out IValue s) && s is Binary bin
            ? bin.ToImmutableArray()
            : ImmutableArray<byte>.Empty;
        return UnmarshalUnsignedTx(dictionary).CombineWithoutVerification(sig);
    }

    [Pure]
    public static Transaction DeserializeTransactionWithoutVerification(byte[] bytes)
    {
        IValue node = Codec.Decode(bytes);
        if (node is Bencodex.Types.Dictionary dict)
        {
            return UnmarshalTransactionWithoutVerification(dict);
        }

        throw new DecodingException(
            $"Expected a {typeof(Bencodex.Types.Dictionary).FullName}, " +
            $"but {node.GetType().Name} given."
        );
    }

    [Pure]
    internal static Transaction DeserializeTransactionWithoutVerification(
        ImmutableArray<byte> bytes)
    {
        byte[] arrayView = Unsafe.As<ImmutableArray<byte>, byte[]>(ref bytes);
        return DeserializeTransactionWithoutVerification(arrayView);
    }

    // TODO: SerializeTransaction, DeserializeTransaction
}
