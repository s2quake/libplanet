using GraphQL;
using GraphQL.Execution;
using GraphQL.Types;
using Libplanet.TestUtilities;
using Libplanet.Types;
using static Libplanet.Explorer.Tests.GraphQLTestUtils;

namespace Libplanet.Explorer.Tests.GraphTypes;

public class VoteTypeTest
{
    [Fact]
    public async Task Query()
    {
        var signer = Rand.Signer();
        var blockHash = new BlockHash(new byte[32]);
        var vote = new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.Now,
            Validator = signer.Address,
            ValidatorPower = 123,
            Type = Types.VoteType.PreCommit,
        }.Sign(signer);

        var query =
            @"{
                    height
                    round
                    blockHash
                    timestamp
                    validatorPublicKey
                    validatorPower
                    flag
                    signature
                }";

        var voteType = new Explorer.GraphTypes.VoteType();
        ExecutionResult result = await ExecuteQueryAsync(
            query,
            voteType,
            source: vote);
        Dictionary<string, object> resultData =
            (Dictionary<string, object>)((ExecutionNode)result.Data!)?.ToValue()!;
        Assert.Null(result.Errors);
        Assert.Equal(vote.Height, resultData["height"]);
        Assert.Equal(vote.Round, resultData["round"]);
        Assert.Equal(vote.BlockHash.ToString(), resultData["blockHash"]);
        Assert.Equal(
            new DateTimeOffsetGraphType().Serialize(vote.Timestamp), resultData["timestamp"]);
        Assert.Equal(vote.Validator.ToString(), resultData["validatorPublicKey"]);
        Assert.Equal(vote.ValidatorPower.ToString(), resultData["validatorPower"]);
        Assert.Equal(vote.Type.ToString(), resultData["flag"]);
        Assert.Equal(ByteUtility.Hex(vote.Signature), resultData["signature"]);
    }
}
