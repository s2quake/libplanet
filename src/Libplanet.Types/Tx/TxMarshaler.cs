using System;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Bencodex;
using Bencodex.Json;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Tx;

public static class TxMarshaler
{
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";
    private static readonly Binary UpdatedAddressesKey = new Binary(new byte[] { 0x75 }); // 'u'
    private static readonly Binary TimestampKey = new Binary(new byte[] { 0x74 }); // 't'
    private static readonly Binary GenesisHashKey = new Binary(new byte[] { 0x67 }); // 'g'
    private static readonly Binary MaxGasPriceKey = new Binary(new byte[] { 0x6d }); // 'm'
    private static readonly Binary GasLimitKey = new Binary(new byte[] { 0x6c }); // 'l'
    private static readonly Binary NonceKey = new Binary(new byte[] { 0x6e }); // 'n'
    private static readonly Binary SignerKey = new Binary(new byte[] { 0x73 }); // 's'
    private static readonly Binary SignatureKey = new Binary(new byte[] { 0x53 }); // 'S'
    private static readonly Binary ActionsKey = new Binary(new byte[] { 0x61 }); // 'a'
    private static readonly Codec Codec = new Codec();

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
    public static Bencodex.Types.Dictionary MarshalTxInvoice(this ITxInvoice invoice)
    {
        Bencodex.Types.List updatedAddresses = new Bencodex.Types.List(
            invoice.UpdatedAddresses.Select<Address, IValue>(addr => addr.ToBencodex())
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
            dict = dict.Add(GenesisHashKey, genesisHash.ByteArray);
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
        this ITxSigningMetadata metadata
    ) => Dictionary.Empty
        .Add(NonceKey, metadata.Nonce)
        .Add(SignerKey, metadata.Signer.ToBencodex());

    [Pure]
    public static Dictionary MarshalUnsignedTx(this IUnsignedTx unsignedTx)
        => (Dictionary)unsignedTx.MarshalTxInvoice()
            .AddRange(unsignedTx.MarshalTxSigningMetadata());

    [Pure]
    public static string SerializeUnsignedTx(this IUnsignedTx unsignedTx)
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
        => transaction.MarshalUnsignedTx().Add(SignatureKey, transaction.Signature);

    [Pure]
    public static ITxInvoice UnmarshalTxInvoice(Bencodex.Types.Dictionary dictionary)
    {
        return new TxInvoice
        {
            Actions = dictionary.TryGetValue(ActionsKey, out IValue av) && av is List list
                ? list.ToImmutableArray()
                : [],
            GenesisHash = dictionary.TryGetValue(GenesisHashKey, out IValue gv)
                ? new BlockHash(gv)
                : null,
            UpdatedAddresses = ImmutableSortedSet.Create(
                [.. ((List)dictionary[UpdatedAddressesKey]).Select(Address.Create)]),
            Timestamp = DateTimeOffset.ParseExact(
                (Text)dictionary[TimestampKey],
                TimestampFormat,
                CultureInfo.InvariantCulture).ToUniversalTime(),
            MaxGasPrice = dictionary.TryGetValue(MaxGasPriceKey, out IValue mgpv)
                ? FungibleAssetValue.Create(mgpv)
                : null,
            GasLimit = dictionary.TryGetValue(GasLimitKey, out IValue glv)
                ? (long)(Bencodex.Types.Integer)glv
                : null,
        };
    }

    [Pure]
    public static ITxSigningMetadata UnmarshalTxSigningMetadata(
        Bencodex.Types.Dictionary dictionary
    ) =>
        new TxSigningMetadata(
            signer: Address.Create(dictionary[SignerKey]),
            nonce: (Bencodex.Types.Integer)dictionary[NonceKey]
        );

    [Pure]
    public static UnsignedTx UnmarshalUnsignedTx(Bencodex.Types.Dictionary dictionary) =>
        new UnsignedTx(
            invoice: UnmarshalTxInvoice(dictionary),
            signingMetadata: UnmarshalTxSigningMetadata(dictionary));

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
    public static IUnsignedTx DeserializeUnsignedTx(byte[] bytes)
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
    public static IUnsignedTx DeserializeUnsignedTx(string json)
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
