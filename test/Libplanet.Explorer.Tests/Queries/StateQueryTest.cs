using System.Threading.Tasks;
using GraphQL;
using GraphQL.Execution;
using Libplanet.Explorer.Queries;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types.Assets;
using static Libplanet.Explorer.Tests.GraphQLTestUtils;
using Fixture = Libplanet.Explorer.Tests.Fixtures.BlockChainStatesFixture;

namespace Libplanet.Explorer.Tests.Queries;

public partial class StateQueryTest
{
    [Fact]
    public async Task World()
    {
        (var source, var blockHash, _) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            world(blockHash: ""{ByteUtility.Hex(blockHash.Bytes)}"") {{
                stateRootHash
                legacy
                version
            }}
        }}
        ", source: source);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> states =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["world"]);
        Assert.NotNull(states["stateRootHash"]);

        Assert.False((bool)states["legacy"]);
    }

    [Fact]
    public async Task WorldByBlockHashThenAccountThenStateAndStates()
    {
        (var source, var blockHash, _) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            world (blockHash: ""{ByteUtility.Hex(blockHash.Bytes)}"") {{
                account (address: ""0x1000000000000000000000000000000000000000"") {{
                    state (address: ""{ByteUtility.Hex(Fixture.Address.Bytes)}"") {{
                        hex
                    }}
                    states (addresses: [
                        ""{ByteUtility.Hex(Fixture.Address.Bytes)}"",
                        ""0x0000000000000000000000000000000000000000""
                    ]) {{
                        hex
                    }}
                }}
            }}
        }}
        ", source: source);

        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> account =
            Assert.IsAssignableFrom<IDictionary<string, object>>(
                Assert.IsAssignableFrom<IDictionary<string, object>>(
                    resultDict["world"])["account"]);

        IDictionary<string, object> state =
            Assert.IsAssignableFrom<IDictionary<string, object>>(account["state"]);
        Assert.Equal(
            ByteUtility.Hex(ModelSerializer.SerializeToBytes(Fixture.Value)),
            Assert.IsAssignableFrom<string>(state["hex"]));

        object[] states =
            Assert.IsAssignableFrom<object[]>(account["states"]);
        Assert.Equal(2, states.Length);
        Assert.Equal(
            ByteUtility.Hex(ModelSerializer.SerializeToBytes(Fixture.Value)),
            Assert.IsAssignableFrom<string>(
                Assert.IsAssignableFrom<IDictionary<string, object>>(states[0])["hex"]));
        Assert.Null(states[1]);
    }

    [Fact]
    public async Task WorldByBlockHashThenBalance()
    {
        (var source, var blockHash, _) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            world (blockHash: ""{ByteUtility.Hex(blockHash.Bytes)}"") {{
                balance (
                    address: ""{ByteUtility.Hex(Fixture.Address.Bytes)}""
                    currency: {{
                        ticker: ""ABC""
                        decimalPlaces: 2
                        minters: null }}) {{
                    string
                }}
            }}
        }}
        ", source: source);

        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> world =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["world"]);
        IDictionary<string, object> balance =
            Assert.IsAssignableFrom<IDictionary<string, object>>(world["balance"]);
        Assert.Equal(
            Fixture.Amount.ToString(),
            Assert.IsAssignableFrom<string>(balance["string"]));
    }

    [Fact]
    public async Task WorldByBlockHashThenTotalSupply()
    {
        (var source, var blockHash, _) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            world (blockHash: ""{ByteUtility.Hex(blockHash.Bytes)}"") {{
                totalSupply (currency: {{
                    ticker: ""ABC""
                    decimalPlaces: 2
                    minters: null}}) {{
                    string
                }}
            }}
        }}
        ", source: source);

        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> totalSupply =
            Assert.IsAssignableFrom<IDictionary<string, object>>(
                Assert.IsAssignableFrom<IDictionary<string, object>>(
                    resultDict["world"])["totalSupply"]);
        FungibleAssetValue expectedTotalSupply
            = Fixture.Amount + Fixture.AdditionalSupply;
        Assert.Equal(
            expectedTotalSupply.ToString(),
            Assert.IsAssignableFrom<string>(totalSupply["string"]));
    }

    [Fact]
    public async Task WorldByBlockHashThenValidatorSet()
    {
        (var source, var blockHash, _) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            world (blockHash: ""{ByteUtility.Hex(blockHash.Bytes)}"") {{
                validatorSet {{
                    hex
                }}
            }}
        }}
        ", source: source);

        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> validatorSet =
            Assert.IsAssignableFrom<IDictionary<string, object>>(
                Assert.IsAssignableFrom<IDictionary<string, object>>(
                    resultDict["world"])["validatorSet"]);
        Assert.Equal(
            ByteUtility.Hex(ModelSerializer.SerializeToBytes(Fixture.Validators)),
            Assert.IsAssignableFrom<string>(validatorSet["hex"]));
    }

    [Fact]
    public async Task WorldByStateRootHashThenAccountThenStateAndStates()
    {
        (var source, _, var stateRootHash) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            world (stateRootHash: ""{ByteUtility.Hex(stateRootHash.Bytes)}"") {{
                account (address: ""0x1000000000000000000000000000000000000000"") {{
                    state (address: ""{ByteUtility.Hex(Fixture.Address.Bytes)}"") {{
                        hex
                    }}
                    states (addresses: [
                        ""{ByteUtility.Hex(Fixture.Address.Bytes)}""
                        ""0x0000000000000000000000000000000000000000""
                    ]) {{
                        hex
                    }}
                }}
            }}
        }}
        ", source: source);

        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> account =
            Assert.IsAssignableFrom<IDictionary<string, object>>(
                Assert.IsAssignableFrom<IDictionary<string, object>>(
                    resultDict["world"])["account"]);

        IDictionary<string, object> state =
            Assert.IsAssignableFrom<IDictionary<string, object>>(account["state"]);
        Assert.Equal(
            ByteUtility.Hex(ModelSerializer.SerializeToBytes(Fixture.Value)),
            Assert.IsAssignableFrom<string>(state["hex"]));

        object[] states =
            Assert.IsAssignableFrom<object[]>(account["states"]);
        Assert.Equal(2, states.Length);
        Assert.Equal(
            ByteUtility.Hex(ModelSerializer.SerializeToBytes(Fixture.Value)),
            Assert.IsAssignableFrom<string>(
                Assert.IsAssignableFrom<IDictionary<string, object>>(states[0])["hex"]));
        Assert.Null(states[1]);
    }

    [Fact]
    public async Task WorldByBlockHashThenAccountsThenStateAndStates()
    {
        (var source, var blockHash, _) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            world (blockHash: ""{ByteUtility.Hex(blockHash.Bytes)}"") {{
                accounts (addresses: [""0x1000000000000000000000000000000000000000""]) {{
                    state (address: ""{ByteUtility.Hex(Fixture.Address.Bytes)}"") {{
                        hex
                    }}
                    states (addresses: [
                        ""{ByteUtility.Hex(Fixture.Address.Bytes)}""
                        ""0x0000000000000000000000000000000000000000""]) {{
                        hex
                    }}
                }}
            }}
        }}
        ", source: source);

        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        object[] accounts =
            Assert.IsAssignableFrom<object[]>(
                Assert.IsAssignableFrom<IDictionary<string, object>>(
                    resultDict["world"])["accounts"]);

        IDictionary<string, object> account =
            Assert.IsAssignableFrom<IDictionary<string, object>>(Assert.Single(accounts));
        IDictionary<string, object> state =
            Assert.IsAssignableFrom<IDictionary<string, object>>(account["state"]);
        Assert.Equal(
            ByteUtility.Hex(ModelSerializer.SerializeToBytes(Fixture.Value)),
            Assert.IsAssignableFrom<string>(state["hex"]));

        object[] states =
            Assert.IsAssignableFrom<object[]>(account["states"]);
        Assert.Equal(2, states.Length);
        Assert.Equal(
            ByteUtility.Hex(ModelSerializer.SerializeToBytes(Fixture.Value)),
            Assert.IsAssignableFrom<string>(
                Assert.IsAssignableFrom<IDictionary<string, object>>(states[0])["hex"]));
        Assert.Null(states[1]);
    }
}
