using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Bencodex.Json;
using Libplanet.Serialization;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Tx;

[Model(Version = 1)]
public sealed record class UnsignedTx
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new BencodexJsonConverter(),
        },
    };

    [Property(0)]
    public required TxInvoice Invoice { get; init; }

    [Property(1)]
    public required TxSigningMetadata SigningMetadata { get; init; }

    public ImmutableSortedSet<Address> UpdatedAddresses => Invoice.UpdatedAddresses;

    public DateTimeOffset Timestamp => Invoice.Timestamp;

    public BlockHash? GenesisHash => Invoice.GenesisHash;

    public ImmutableArray<ActionBytecode> Actions => Invoice.Actions;

    public FungibleAssetValue? MaxGasPrice => Invoice.MaxGasPrice;

    public long? GasLimit => Invoice.GasLimit;

    public long Nonce => SigningMetadata.Nonce;

    public Address Signer => SigningMetadata.Signer;

    public static UnsignedTx Create(TxInvoice invoice, TxSigningMetadata signingMetadata) => new()
    {
        Invoice = invoice,
        SigningMetadata = signingMetadata,
    };

    public ImmutableArray<byte> CreateSignature(PrivateKey privateKey)
    {
        if (!privateKey.Address.Equals(Signer))
        {
            throw new ArgumentException(
                "The given private key does not correspond to the public key.",
                paramName: nameof(privateKey));
        }

        byte[] sig = privateKey.Sign(CreateMessage());
        ImmutableArray<byte> movedImmutableArray =
            Unsafe.As<byte[], ImmutableArray<byte>>(ref sig);
        return movedImmutableArray;
    }

    public bool VerifySignature(ImmutableArray<byte> signature) =>
        PublicKey.Verify(Signer, [.. CreateMessage()], signature);

    public Transaction Sign(PrivateKey privateKey) => Transaction.Create(this, privateKey);

    public Transaction Verify(ImmutableArray<byte> signature)
        => new() { UnsignedTx = this, Signature = signature };

    public byte[] CreateMessage()
    {
        throw new NotImplementedException(
            "The BencodexJsonConverter is not implemented yet.");
        var dict = ModelSerializer.SerializeToBytes(this);
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });
        // new BencodexJsonConverter().Write(writer, dict, SerializerOptions);

        ms.Position = 0;
        using var sr = new StreamReader(ms);
        var json = sr.ReadToEnd();
        return Encoding.UTF8.GetBytes(json);
    }
}
