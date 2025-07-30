using GraphQL;
using GraphQL.Types;
using GraphQL.Execution;
using Libplanet.Types;
using Libplanet.Types.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Explorer.GraphTypes;
using static Libplanet.Explorer.Tests.GraphQLTestUtils;
using System.Threading.Tasks;

namespace Libplanet.Explorer.Tests.GraphTypes;

public class BlockCommitTypeTest
{
    [Fact]
    public async Task Query()
    {

        var privateKey = new PrivateKey();
        var blockHash = new BlockHash(new byte[32]);
        var vote = new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.Now,
            ValidatorPublicKey = privateKey.PublicKey,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(privateKey);

        var blockCommit = new BlockCommit
        {
            Height = 1,
            Round = 0,
            BlockHash = blockHash,
            Votes = ImmutableArray.Create(vote),
        };

        var query =
            @"{
                    height
                    round
                    blockHash
                    votes {
                        height
                        round
                        blockHash
                        timestamp
                        validatorPublicKey
                        flag
                        signature
                    }
                }";

        var blockCommitType = new BlockCommitType();
        ExecutionResult result = await ExecuteQueryAsync(
            query,
            blockCommitType,
            source: blockCommit
        );
        Dictionary<string, object> resultData =
            (Dictionary<string, object>)((ExecutionNode)result.Data!)?.ToValue()!;
        Assert.Null(result.Errors);
        Assert.Equal(blockCommit.Height, resultData["height"]);
        Assert.Equal(blockCommit.Round, resultData["round"]);
        Assert.Equal(blockCommit.BlockHash.ToString(), resultData["blockHash"]);
        var expectedVotes = new object[] {
            new Dictionary<string, object?>()
            {
                { "height", vote.Height },
                { "round", vote.Round },
                { "blockHash", vote.BlockHash.ToString() },
                { "timestamp", new DateTimeOffsetGraphType().Serialize(vote.Timestamp) },
                { "validatorPublicKey", vote.ValidatorPublicKey.ToString() },
                { "flag", vote.Flag.ToString() },
                { "signature", ByteUtil.Hex(vote.Signature) },
            }
        };
        Assert.Equal(expectedVotes, resultData["votes"]);
    }
}
