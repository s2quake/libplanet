using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Tx;

public sealed record class TxMetadata
{
    internal static readonly Binary CustomActionsKey = new("a"u8.ToArray()); // 'a'
    internal static readonly Binary SystemActionKey = new("A"u8.ToArray()); // 'A'
    internal static readonly Binary SignatureKey = new("S"u8.ToArray()); // 'S'
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";
    private static readonly Binary NonceKey = new("n"u8.ToArray()); // 'n'
    private static readonly Binary SignerKey = new("s"u8.ToArray()); // 's'
    private static readonly Binary GenesisHashKey = new("g"u8.ToArray()); // 'g'
    private static readonly Binary UpdatedAddressesKey = new("u"u8.ToArray()); // 'u'
    private static readonly Binary TimestampKey = new("t"u8.ToArray()); // 't'

    public static TxMetadata Create(Transaction metadata)
    {
        return new TxMetadata
        {
            Nonce = metadata.Nonce,
            GenesisHash = metadata.GenesisHash,
            UpdatedAddresses = metadata.UpdatedAddresses,
            Signer = metadata.Signer,
            Timestamp = metadata.Timestamp,
        };
    }

    public static TxMetadata Create(IValue value)
    {
        if (value is not Dictionary dictionary)
        {
            throw new ArgumentException("Serialized value must be a dictionary.", nameof(value));
        }

        return new TxMetadata
        {
            Nonce = (Integer)dictionary[NonceKey],
            GenesisHash = dictionary.TryGetValue(GenesisHashKey, out IValue v)
                ? new BlockHash(v)
                : (BlockHash?)null,
            UpdatedAddresses = [.. ((List)dictionary[UpdatedAddressesKey]).Select(Address.Create)],
            Signer = new Address(((Binary)dictionary[SignerKey]).ByteArray),
            Timestamp = DateTimeOffset.ParseExact(
                (Text)dictionary[TimestampKey],
                TimestampFormat,
                CultureInfo.InvariantCulture
            ).ToUniversalTime(),
        };
    }

    public long Nonce { get; init; }

    /// <summary>
    /// A <see cref="Address"/> of the account who signs this transaction.
    /// </summary>
    /// <remarks>This is automatically derived from <see cref="PublicKey"/>.</remarks>
    public required Address Signer { get; init; }

    /// <summary>
    /// An approximated list of addresses whose states would be affected by actions in this
    /// transaction.  However, it could be wrong.
    /// </summary>
    /// <remarks>See also https://github.com/planetarium/libplanet/issues/368 .</remarks>
    public ImmutableSortedSet<Address> UpdatedAddresses { get; init; } = [];

    /// <summary>
    /// The time this transaction is created and signed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// A <see cref="BlockHash"/> value of the genesis which this transaction is made
    /// from.  This can be <see langword="null"/> iff the transaction is contained in
    /// the genesis block.
    /// </summary>
    public BlockHash? GenesisHash { get; init; }

    public IValue ToBencodex()
    {
        List updatedAddresses =
        [
            .. UpdatedAddresses.Select<Address, IValue>(addr => addr.ToBencodex())
        ];
        string timestamp = Timestamp
            .ToUniversalTime()
            .ToString(TimestampFormat, CultureInfo.InvariantCulture);
        Bencodex.Types.Dictionary dict = Dictionary.Empty
            .Add(NonceKey, Nonce)
            .Add(SignerKey, Signer.ToBencodex())
            .Add(UpdatedAddressesKey, updatedAddresses)
            .Add(TimestampKey, timestamp);

        if (GenesisHash is { } genesisHash)
        {
            dict = dict.Add(GenesisHashKey, genesisHash.ByteArray);
        }

        return dict;
    }

    public override string ToString()
    {
        return nameof(TxMetadata) + " {\n" +
            $"  {nameof(Nonce)} = {Nonce},\n" +
            $"  {nameof(Signer)} = {Signer},\n" +
            $"  {nameof(UpdatedAddresses)} = ({UpdatedAddresses.Count})" +
            (UpdatedAddresses.Any()
                ? $"\n    {string.Join("\n    ", UpdatedAddresses)};\n"
                : ";\n") +
            $"  {nameof(Timestamp)} = {Timestamp},\n" +
            $"  {nameof(GenesisHash)} = {GenesisHash?.ToString() ?? "(null)"},\n" +
            "}";
    }
}
