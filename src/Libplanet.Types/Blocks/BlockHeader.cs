using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockHeader
{
    public const int CurrentProtocolVersion = 0;

    private static readonly TimeSpan TimestampThreshold = TimeSpan.FromSeconds(15);

    public static BlockHeader Empty { get; } = new();

    [Property(0)]
    public int ProtocolVersion { get; init; } = CurrentProtocolVersion;

    [Property(1)]
    public long Height { get; init; }

    [Property(2)]
    public DateTimeOffset Timestamp { get; init; }

    [Property(3)]
    public Address Proposer { get; init; }

    [Property(4)]
    public BlockHash PreviousHash { get; init; }

    [Property(5)]
    public BlockCommit LastCommit { get; init; } = BlockCommit.Empty;

    public static ImmutableArray<byte> MakeSignature(PrivateKey privateKey, HashDigest<SHA256> stateRootHash)
    {
        var msg = ModelSerializer.SerializeToBytes(stateRootHash);
        var sig = privateKey.Sign(msg);
        return ImmutableArray.Create(sig);
    }

    public void ValidateTimestamp() => ValidateTimestamp(DateTimeOffset.UtcNow);

    public void ValidateTimestamp(DateTimeOffset currentTime)
    {
        if (currentTime + TimestampThreshold < Timestamp)
        {
            var message = $"The block #{Height}'s timestamp " +
                $"({Timestamp}) is later than now " +
                $"({currentTime}, threshold: {TimestampThreshold}).";
            throw new InvalidOperationException(message);
        }
    }

    public BlockHash DeriveBlockHash(in HashDigest<SHA256> stateRootHash, in ImmutableArray<byte> signature)
    {
        var list = new List(
            ModelSerializer.Serialize(this),
            ModelSerializer.Serialize(stateRootHash),
            ModelSerializer.Serialize(signature));
        return BlockHash.DeriveFrom(BencodexUtility.Encode(list));
    }

    public bool VerifySignature(ImmutableArray<byte> signature, HashDigest<SHA256> stateRootHash)
    {
        var message = ModelSerializer.SerializeToBytes(stateRootHash).ToImmutableArray();
        return PublicKey.Verify(Proposer, message, signature);
    }

    public HashDigest<SHA256> DerivePreEvaluationHash()
        => HashDigest<SHA256>.DeriveFrom(ModelSerializer.SerializeToBytes(this));
}
