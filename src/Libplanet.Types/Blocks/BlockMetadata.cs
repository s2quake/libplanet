using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Types.Crypto;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockMetadata
{
    public const int CurrentProtocolVersion = 9;

    private static readonly TimeSpan TimestampThreshold = TimeSpan.FromSeconds(15);

    [Property(0)]
    public int ProtocolVersion { get; init; }

    [Property(1)]
    public long Height { get; init; }

    [Property(2)]
    public DateTimeOffset Timestamp { get; init; }

    [Property(3)]
    [NonDefault]
    public Address Proposer { get; init; }

    [Property(4)]
    public BlockHash PreviousHash { get; init; }

    [Property(5)]
    public HashDigest<SHA256> TxHash { get; init; }

    [Property(6)]
    public BlockCommit LastCommit { get; init; } = BlockCommit.Empty;

    [Property(7)]
    public HashDigest<SHA256> EvidenceHash { get; init; }

    public static explicit operator BlockMetadata(BlockHeader header)
    {
        return new BlockMetadata
        {
            ProtocolVersion = header.ProtocolVersion,
            Height = header.Height,
            Timestamp = header.Timestamp,
            Proposer = header.Proposer,
            PreviousHash = header.PreviousHash,
            TxHash = header.TxHash,
            LastCommit = header.LastCommit,
            EvidenceHash = header.EvidenceHash,
        };
    }

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
