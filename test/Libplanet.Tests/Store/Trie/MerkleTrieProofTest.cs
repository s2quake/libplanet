using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Store.Trie.Nodes;
using Libplanet.Types;

namespace Libplanet.Tests.Store.Trie;

public class MerkleTrieProofTest
{
    public readonly MemoryTable KeyValueStore = [];
    public readonly TrieStateStore StateStore;

    // "1b16b1df538ba12dc3f97edbb85caa7050d46c148134290feba80f8236c83db9"
    public readonly ITrie EmptyTrie;

    // Note that * denotes an existence of a value and L denotes the encoded length.
    // Structure:
    // [HashNode:L35]
    // |
    // |
    // [ShortNode:L24]
    // |
    // | 0
    // [FullNode:L19]
    // |
    // |___________________
    // | 0                 | 1
    // [ValueNode:*:L08]   [ValueNode:*:L08]
    public readonly ITrie HalfTrie;

    public readonly HashDigest<SHA256> HalfTrieHash = new HashDigest<SHA256>(
        ByteUtility.ParseHex("6cc5c2ca1b7b146268f0d930c58c7e5441b807e72cf16d56f52c869a594b17bf"));

    // An uncommitted instance of FullTrie.
    public readonly ITrie UncommittedTrie;

    // Note that * denotes an existence of a value and L denotes the encoded length.
    // Structure:
    // [HashNode:L35]
    // |
    // |
    // [ShortNode:L40]
    // |
    // | 0
    // [HashNode:35]-------[FullNode:L60]
    //                     |
    // ____________________|___________________
    // | 0                                    | 1
    // [HashNode:L35]------[FullNode:*:L74]   [ValueNode:*:L08]
    // ____________________|
    // | 0                 | 1
    // [ShortNode:L15]     [HashNode:L35]-----[ShortNode:L40]
    // |                                      |
    // | 0                                    | 0
    // [ValueNode:*:L10]                      [HashNode:L35]-----[ValueNode:*:L39]
    public readonly ITrie FullTrie;

    public readonly HashDigest<SHA256> FullTrieHash = new HashDigest<SHA256>(
        ByteUtility.ParseHex("979a00921d42d2ca63e98c1c2ac07f0eacbb99e363b8f2f7f8e4d19c854b6c20"));

    public readonly KeyBytes K00 = KeyBytes.Parse("00");
    public readonly KeyBytes K01 = KeyBytes.Parse("01");
    public readonly KeyBytes K0000 = KeyBytes.Parse("0000");
    public readonly KeyBytes K0010 = KeyBytes.Parse("0010");

    public readonly string V00 = "00";
    public readonly string V01 = "01";
    public readonly string V0000 = "0000";
    public readonly string V0010 = "00000000000000000000000000000010";

    public readonly IReadOnlyList<INode> P00;
    public readonly IReadOnlyList<INode> P01;
    public readonly IReadOnlyList<INode> P0000;
    public readonly IReadOnlyList<INode> P0010;

    public MerkleTrieProofTest()
    {
        StateStore = new TrieStateStore(KeyValueStore);
        ITrie trie = StateStore.GetStateRoot(default);
        EmptyTrie = trie;

        trie = trie.Set(K00, V00);
        trie = trie.Set(K01, V01);
        trie = StateStore.Commit(trie);
        HalfTrie = trie;

        trie = trie.Set(K0000, V0000);
        trie = trie.Set(K0010, V0010);
        UncommittedTrie = trie;
        trie = StateStore.Commit(trie);
        FullTrie = trie;

        Nibbles n0 = new Nibbles(new byte[] { 0 }.ToImmutableArray());

        INode proofNode0010 = new ValueNode { Value = V0010 };
        INode proofNode001 = new ShortNode { Key = n0, Value = ToHashNode(proofNode0010) };
        INode proofNode00 = new FullNode
        {
            Children = ImmutableSortedDictionary<byte, INode>.Empty
                .Add(0, new ShortNode { Key = n0, Value = new ValueNode { Value = V0000 } })
                .Add(1, ToHashNode(proofNode001)),
            Value = new ValueNode { Value = V00 },
        };
        INode proofNode0 = new FullNode
        {
            Children = ImmutableSortedDictionary<byte, INode>.Empty
                .Add(0, ToHashNode(proofNode00))
                .Add(1, new ValueNode { Value = V01 }),
        };
        INode proofRoot = new ShortNode { Key = n0, Value = ToHashNode(proofNode0) };
        P00 = new List<INode>() { proofRoot, proofNode0, proofNode00 };
        P01 = new List<INode>() { proofRoot, proofNode0 };
        P0000 = new List<INode>() { proofRoot, proofNode0, proofNode00 };
        P0010 = new List<INode>()
            { proofRoot, proofNode0, proofNode00, proofNode001, proofNode0010 };
    }

    [Fact]
    public void CheckFixtures()
    {
        Assert.Equal(HalfTrieHash, HalfTrie.Hash);
        Assert.Equal(FullTrieHash, FullTrie.Hash);
    }

    [Fact]
    public void GetProof()
    {
        var proof = ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(K00, V00);
        Assert.Equal(P00, proof);

        proof = ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(K01, V01);
        Assert.Equal(P01, proof);

        proof = ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(K0000, V0000);
        Assert.Equal(P0000, proof);

        proof = ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(K0010, V0010);
        Assert.Equal(P0010, proof);

        KeyBytes k = KeyBytes.Parse(string.Empty);
        var v = string.Empty;
        var trie = StateStore.Commit(EmptyTrie.Set(k, v));
        proof = ((Libplanet.Store.Trie.Trie)trie).GenerateProof(k, v);
        Assert.Equal(v, Assert.IsType<ValueNode>(Assert.Single(proof)).Value);

        trie = StateStore.Commit(FullTrie.Set(k, v));
        proof = ((Libplanet.Store.Trie.Trie)trie).GenerateProof(k, v);
        Assert.Equal(
            v,
            Assert.IsType<ValueNode>(
                Assert.IsType<FullNode>(
                    Assert.Single(proof)).Value).Value);
    }

    [Fact]
    public void DifferentRootsProduceDifferentProofs()
    {
        var proof1 = ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(K00, V00);
        var proof2 = ((Libplanet.Store.Trie.Trie)HalfTrie).GenerateProof(K00, V00);
        Assert.NotEqual(proof1.Count, proof2.Count);

        Assert.True(
            Libplanet.Store.Trie.Trie.ValidateProof(FullTrieHash, proof1, K00, V00));
        Assert.False(
            Libplanet.Store.Trie.Trie.ValidateProof(FullTrieHash, proof2, K00, V00));
        Assert.False(
            Libplanet.Store.Trie.Trie.ValidateProof(HalfTrieHash, proof1, K00, V00));
        Assert.True(
            Libplanet.Store.Trie.Trie.ValidateProof(HalfTrieHash, proof2, K00, V00));
    }

    [Fact]
    public void InvalidGenerateProofCalls()
    {
        Assert.Contains(
            "recorded",
            Assert.Throws<InvalidOperationException>(
                () => ((Libplanet.Store.Trie.Trie)UncommittedTrie).GenerateProof(K00, V00)).Message);
        Assert.Contains(
            "non-null",
            Assert.Throws<InvalidOperationException>(
                () => ((Libplanet.Store.Trie.Trie)EmptyTrie).GenerateProof(K00, V00)).Message);
        Assert.Contains(
            "does not match",
            Assert.Throws<ArgumentException>(
                () => ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(K00, V01)).Message);
        Assert.Contains(
            "could not be fully resolved",
            Assert.Throws<ArgumentException>(
                () => ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(
                    KeyBytes.Parse("000000"), V0000)).Message);
        Assert.Contains(
            "could not be properly resolved",
            Assert.Throws<ArgumentException>(
                () => ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(
                    KeyBytes.Parse("0020"), V0000)).Message);
    }

    [Fact]
    public void ValidateProof()
    {
        Assert.True(Libplanet.Store.Trie.Trie.ValidateProof(
            FullTrieHash,
            ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(K00, V00),
            K00,
            V00));
        Assert.True(Libplanet.Store.Trie.Trie.ValidateProof(
            FullTrieHash,
            ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(K01, V01),
            K01,
            V01));
        Assert.True(Libplanet.Store.Trie.Trie.ValidateProof(
            FullTrieHash,
            ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(K0000, V0000),
            K0000,
            V0000));
        Assert.True(Libplanet.Store.Trie.Trie.ValidateProof(
            FullTrieHash,
            ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(K0010, V0010),
            K0010,
            V0010));

        Assert.False(Libplanet.Store.Trie.Trie.ValidateProof(
            HalfTrieHash,
            ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(K00, V00),
            K00,
            V00));  // Wrong hash
        Assert.False(Libplanet.Store.Trie.Trie.ValidateProof(
            FullTrieHash,
            ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(K00, V00),
            K00,
            V01));  // Wrong value
        Assert.False(Libplanet.Store.Trie.Trie.ValidateProof(
            FullTrieHash,
            ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(K00, V00),
            K01,
            V00));  // Wrong key
        Assert.False(Libplanet.Store.Trie.Trie.ValidateProof(
            FullTrieHash,
            ((Libplanet.Store.Trie.Trie)FullTrie).GenerateProof(K00, V00),
            K01,
            V01));  // Wrong proof
    }

    private HashNode ToHashNode(INode node) => new()
    {
        Hash = HashDigest<SHA256>.Create(ModelSerializer.SerializeToBytes(node)),
    };
}
