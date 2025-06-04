using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Types;

[Model(Version = 1, TypeName = "Transaction")]
public sealed partial record class Transaction
    : IComparable<Transaction>, IComparable, IHasKey<TxId>
{
    [Property(0)]
    public required TransactionMetadata Metadata { get; init; }

    [Property(1)]
    [NotDefault]
    public required ImmutableArray<byte> Signature { get; init; }

    public TxId Id => new(SHA256.HashData(ModelSerializer.SerializeToBytes(this)));

    public long Nonce => Metadata.Nonce;

    public Address Signer => Metadata.Signer;

    public ImmutableArray<ActionBytecode> Actions => Metadata.Actions;

    public FungibleAssetValue? MaxGasPrice => Metadata.MaxGasPrice;

    public long GasLimit => Metadata.GasLimit;

    public DateTimeOffset Timestamp => Metadata.Timestamp;

    public BlockHash GenesisHash => Metadata.GenesisHash;

    TxId IHasKey<TxId>.Key => Id;

    public bool Equals(Transaction? other) => Id.Equals(other?.Id);

    public override int GetHashCode() => Id.GetHashCode();

    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        Transaction other => CompareTo(other),
        _ => throw new ArgumentException($"Argument {nameof(obj)} is not ${nameof(Transaction)}.", nameof(obj)),
    };

    public int CompareTo(Transaction? other) => other switch
    {
        null => 1,
        _ => Nonce == other.Nonce ? Id.CompareTo(other.Id) : Nonce.CompareTo(other.Nonce),
    };
}
