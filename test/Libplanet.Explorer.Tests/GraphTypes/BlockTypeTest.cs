using System.Security.Cryptography;
using GraphQL;
using GraphQL.Execution;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Explorer.Tests.Queries;
using Libplanet.Serialization;
using Libplanet.Data;
using Libplanet.Types;
using static Libplanet.Explorer.Tests.GraphQLTestUtils;
using Libplanet.TestUtilities;

namespace Libplanet.Explorer.Tests.GraphTypes;

public class BlockTypeTest
{
    [Fact]
    public async Task Query()
    {
        var signer = RandomUtility.Signer();
        var lastBlockHash = new BlockHash(RandomUtility.Bytes(HashDigest<SHA256>.Size));
        var lastVotes = ImmutableArray.Create(
            new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = lastBlockHash,
                Timestamp = DateTimeOffset.Now,
                Validator = signer.Address,
                ValidatorPower = BigInteger.One,
                Type = Types.VoteType.PreCommit,
            }.Sign(signer));
        var lastBlockCommit = new BlockCommit
        {
            Height = 1,
            Round = 0,
            BlockHash = lastBlockHash,
            Votes = lastVotes,
        };
        var preEval = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 2,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = signer.Address,
                PreviousBlockHash = lastBlockHash,
                PreviousBlockCommit = lastBlockCommit,
            },
        };
        var stateRootHash =
            new HashDigest<SHA256>(RandomUtility.Bytes(HashDigest<SHA256>.Size));
        // var signature = RawBlock.MakeSignature(privateKey, stateRootHash);
        // var hash = preEval.Header.DeriveBlockHash(stateRootHash, signature);
        // var block = new Block { Header = new BlockHeader(), Content = new BlockContent() };
        var block = preEval.Sign(signer);

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

        var store = new Libplanet.Data.Repository(new MemoryDatabase());
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
            ByteUtility.Hex(block.BlockHash.Bytes.ToArray()),
            resultData["hash"]);
        Assert.Equal(
            block.Proposer.ToString(),
            resultData["miner"]);
        Assert.Equal(
            new DateTimeOffsetGraphType().Serialize(block.Timestamp),
            resultData["timestamp"]);
        Assert.Equal(
            ByteUtility.Hex(block.PreviousStateRootHash.Bytes.ToArray()),
            resultData["stateRootHash"]);

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
                        { "validatorPublicKey", lastVotes[0].Validator.ToString() },
                        { "validatorPower", lastVotes[0].ValidatorPower.ToString() },
                        { "flag", lastVotes[0].Type.ToString() },
                        { "signature", ByteUtility.Hex(lastVotes[0].Signature) },
                    }
                }
            },
        };

        Assert.Equal(expectedLastCommit, resultData["lastCommit"]);

        Assert.Equal(
            block.Version,
            resultData["protocolVersion"]);

        Assert.Equal(
            block,
            ModelSerializer.DeserializeFromBytes<Block>(ByteUtility.ParseHex((string)resultData["raw"])));
    }
}
