using System.Security.Cryptography;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Execution;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Explorer.Tests.Queries;
using Libplanet.Serialization;
using Libplanet.Store;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using static Libplanet.Explorer.Tests.GraphQLTestUtils;

namespace Libplanet.Explorer.Tests.GraphTypes;

public class BlockTypeTest
{
    [Fact]
    public async Task Query()
    {
        var privateKey = new PrivateKey();
        var lastBlockHash = new BlockHash(TestUtils.GetRandomBytes(HashDigest<SHA256>.Size));
        var lastVotes = ImmutableArray.Create(
            new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = lastBlockHash,
                Timestamp = DateTimeOffset.Now,
                ValidatorPublicKey = privateKey.PublicKey,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(privateKey));
        var lastBlockCommit = new BlockCommit
        {
            Height = 1,
            Round = 0,
            BlockHash = lastBlockHash,
            Votes = lastVotes,
        };
        var preEval = RawBlock.Propose(
            new BlockMetadata
            {
                Height = 2,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = privateKey.Address,
                PreviousHash = lastBlockHash,
                LastCommit = lastBlockCommit,
            },
            new BlockContent
            {
            });
        var stateRootHash =
            new HashDigest<SHA256>(TestUtils.GetRandomBytes(HashDigest<SHA256>.Size));
        var signature = BlockMetadata.MakeSignature(privateKey, stateRootHash);
        var hash = preEval.Metadata.DeriveBlockHash(stateRootHash, signature);
        var block = new Block { Header = new BlockHeader(), Content = new BlockContent() };

        // FIXME We need to test for `previousBlock` field too.
        var query =
            @"{
                    index
                    hash
                    miner
                    publicKey
                    timestamp
                    stateRootHash
                    signature
                    preEvaluationHash
                    lastCommit
                    {
                        height
                        round
                        blockHash
                        votes
                        {
                            height
                            round
                            blockHash
                            timestamp
                            validatorPublicKey
                            validatorPower
                            flag
                            signature
                        }
                    }
                    protocolVersion
                    raw
                }";

        var store = new MemoryStore();
        var blockType = new BlockType(new MockBlockChainContext(store));
        ExecutionResult result = await ExecuteQueryAsync(
            query,
            blockType,
            source: block);
        Dictionary<string, object> resultData =
            (Dictionary<string, object>)((ExecutionNode)result.Data!)?.ToValue()!;
        Assert.Null(result.Errors);
        Assert.Equal(block.Height, resultData["index"]);
        Assert.Equal(
            ByteUtility.Hex(block.Hash.Bytes.ToArray()),
            resultData["hash"]);
        Assert.Equal(
            block.Proposer.ToString(),
            resultData["miner"]);
        Assert.Equal(
            new DateTimeOffsetGraphType().Serialize(block.Timestamp),
            resultData["timestamp"]);
        Assert.Equal(
            ByteUtility.Hex(block.StateRootHash.Bytes.ToArray()),
            resultData["stateRootHash"]);
        Assert.Equal(
            ByteUtility.Hex(block.RawHash.Bytes.ToArray()),
            resultData["preEvaluationHash"]);

        var expectedLastCommit = new Dictionary<string, object>()
        {
            { "height", lastBlockCommit.Height },
            { "round", lastBlockCommit.Round },
            { "blockHash", lastBlockCommit.BlockHash.ToString() },
            {
                "votes",
                new object[]
                {
                    new Dictionary<string, object?>()
                    {
                        { "height", lastVotes[0].Height },
                        { "round", lastVotes[0].Round },
                        { "blockHash", lastVotes[0].BlockHash.ToString() },
                        {
                            "timestamp",
                            new DateTimeOffsetGraphType().Serialize(lastVotes[0].Timestamp)
                        },
                        { "validatorPublicKey", lastVotes[0].ValidatorPublicKey.ToString() },
                        { "validatorPower", lastVotes[0].ValidatorPower.ToString() },
                        { "flag", lastVotes[0].Flag.ToString() },
                        { "signature", ByteUtility.Hex(lastVotes[0].Signature) },
                    }
                }
            },
        };

        Assert.Equal(expectedLastCommit, resultData["lastCommit"]);

        Assert.Equal(
            block.ProtocolVersion,
            resultData["protocolVersion"]);

        Assert.Equal(
            block,
            ModelSerializer.DeserializeFromBytes<Block>(ByteUtility.ParseHex((string)resultData["raw"])));
    }
}
