using System.Threading.Tasks;
using GraphQL;
using GraphQL.Execution;
using Libplanet.Explorer.Queries;
using Libplanet.Serialization;
using Libplanet.Types;
using static Libplanet.Explorer.Tests.GraphQLTestUtils;
using Fixture = Libplanet.Explorer.Tests.Fixtures.BlockChainStatesFixture;

namespace Libplanet.Explorer.Tests.Queries;

public partial class RawStateQueryTest
{
    private static readonly Codec _codec = new Codec();

    [Fact]
    public async Task StateValue()
    {
        // Check value at address path.
        (var source, _, var stateRootHash) = Fixture.CreateMockBlockChainStates();
        var key
            = "35303033373132623633626161623938303934616436373865613262323462636534343564303736";
        ExecutionResult result = await ExecuteQueryAsync<RawStateQuery>($@"
        {{
            trie(stateRootHash: ""{ByteUtility.Hex(stateRootHash.Bytes)}"") {{
                value(key: ""{key}"") {{
                    hex
                }}
            }}
        }}
        ", source: source);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> trie =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["trie"]);
        IDictionary<string, object> value =
            Assert.IsAssignableFrom<IDictionary<string, object>>(trie["value"]);
        Assert.Equal(
            ByteUtility.Hex(_codec.Encode(Fixture.Value)),
            Assert.IsAssignableFrom<string>(value["hex"]));

        result = await ExecuteQueryAsync<RawStateQuery>($@"
        {{
            trie(stateRootHash: ""{ByteUtility.Hex(stateRootHash.Bytes)}"") {{
                value(key: ""5f5f5f"") {{
                    hex
                }}
            }}
        }}
        ", source: source);
        Assert.Null(result.Errors);
        resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        trie =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["trie"]);
        value =
            Assert.IsAssignableFrom<IDictionary<string, object>>(trie["value"]);
        Assert.Equal(
            ByteUtility.Hex(ModelSerializer.SerializeToBytes(Fixture.Validators)),
            Assert.IsAssignableFrom<string>(value["hex"]));
    }
}
