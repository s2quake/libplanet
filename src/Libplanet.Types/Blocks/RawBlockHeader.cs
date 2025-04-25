using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;

namespace Libplanet.Types.Blocks;

public sealed record class RawBlockHeader(
    BlockMetadata Metadata, HashDigest<SHA256> RawHash)
{
    private static readonly Codec Codec = new();

    public BlockMetadata Metadata { get; } = Metadata;

    public int ProtocolVersion => Metadata.ProtocolVersion;

    public long Index => Metadata.Index;

    public DateTimeOffset Timestamp => Metadata.Timestamp;

    public Address Miner => Metadata.Miner;

    public PublicKey? PublicKey => Metadata.PublicKey;

    public BlockHash PreviousHash => Metadata.PreviousHash;

    public HashDigest<SHA256>? TxHash => Metadata.TxHash;

    public BlockCommit? LastCommit => Metadata.LastCommit;

    public HashDigest<SHA256>? EvidenceHash => Metadata.EvidenceHash;

    public HashDigest<SHA256> RawHash { get; } = CheckPreEvaluationHash(Metadata, RawHash);

    public Bencodex.Types.Dictionary MakeCandidateData(
        HashDigest<SHA256> stateRootHash,
        ImmutableArray<byte>? signature = null)
    {
        throw new NotImplementedException();
        // Dictionary dict = Metadata.MakeCandidateData()
        //     .Add("state_root_hash", stateRootHash.ByteArray);
        // if (signature is { } sig)
        // {
        //     dict = dict.Add("signature", sig);
        // }

        // return dict;
    }

    public ImmutableArray<byte> MakeSignature(
        PrivateKey privateKey,
        HashDigest<SHA256> stateRootHash)
    {
        if (PublicKey is null)
        {
            throw new InvalidOperationException(
                "The block with the protocol version < 2 cannot be signed, because it lacks " +
                "its miner's public key so that others cannot verify its signature."
            );
        }
        else if (!privateKey.PublicKey.Equals(PublicKey))
        {
            string m = "The given private key does not match to the proposer's public key." +
                $"Block's public key: {PublicKey}\n" +
                $"Derived public key: {privateKey.PublicKey}\n";
            throw new ArgumentException(m, nameof(privateKey));
        }

        byte[] msg = Codec.Encode(MakeCandidateData(stateRootHash));
        byte[] sig = privateKey.Sign(msg);
        return ImmutableArray.Create(sig);
    }

    public bool VerifySignature(
        ImmutableArray<byte>? signature,
        HashDigest<SHA256> stateRootHash)
    {
        if (PublicKey is { } pubKey && signature is { } sig)
        {
            var msg = Codec.Encode(MakeCandidateData(stateRootHash)).ToImmutableArray();
            return pubKey.Verify(msg, sig);
        }
        else if (PublicKey is null)
        {
            return signature is null;
        }

        return false;
    }

    public BlockHash DeriveBlockHash(
        in HashDigest<SHA256> stateRootHash,
        in ImmutableArray<byte>? signature
    ) =>
        BlockHash.DeriveFrom(Codec.Encode(MakeCandidateData(stateRootHash, signature)));

    private static HashDigest<SHA256> CheckPreEvaluationHash(
        BlockMetadata metadata,
        in HashDigest<SHA256> preEvaluationHash)
    {
        if (metadata.ProtocolVersion < BlockMetadata.PBFTProtocolVersion)
        {
            return preEvaluationHash;
        }
        else
        {
            HashDigest<SHA256> expected = metadata.DerivePreEvaluationHash();
            return expected.Equals(preEvaluationHash)
                ? expected
                : throw new InvalidOperationException(
                    $"Given {nameof(preEvaluationHash)} {preEvaluationHash} does not match " +
                    $"the expected value {expected}.");
        }
    }
}
