using System.Threading.Tasks;
using GraphQL;
using GraphQL.Execution;
using Libplanet.Types;
using Libplanet.Explorer.Queries;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Fixture = Libplanet.Explorer.Tests.Fixtures.BlockChainStatesFixture;
using static Libplanet.Explorer.Tests.GraphQLTestUtils;

namespace Libplanet.Explorer.Tests.Queries;

public partial class StateQueryTest
{
    [Fact]
    public async Task States()
    {
        (var source, var blockHash, _) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            states(
                addresses: [
                   ""{ByteUtil.Hex(Fixture.Address.Bytes)}""
                   ""0x0000000000000000000000000000000000000000""
                ]
                offsetBlockHash:
                    ""{ByteUtil.Hex(blockHash.Bytes)}""
            )
        }}
        ", source: source);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        object[] states =
            Assert.IsAssignableFrom<object[]>(resultDict["states"]);
        Assert.Equal(2, states.Length);
        Assert.Equal(new[] { _codec.Encode(Fixture.Value), null }, states);
    }

    [Fact]
    public async Task Balance()
    {
        (var source, var blockHash, _) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            balance(
                owner: ""{ByteUtil.Hex(Fixture.Address.Bytes)}""
                currency: {{ ticker: ""ABC"", decimalPlaces: 2 }}
                offsetBlockHash: ""{ByteUtil.Hex(blockHash.Bytes)}"") {{
                currency {{ ticker, hash }}
                sign
                majorUnit
                minorUnit
                quantity
                string
            }}
        }}
        ", source: source);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> balanceDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["balance"]);
        IDictionary<string, object> currencyDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(balanceDict["currency"]);
        Assert.Equal(Fixture.Currency.Ticker, currencyDict["ticker"]);
        Assert.Equal(ByteUtil.Hex(Fixture.Currency.Hash.Bytes), currencyDict["hash"]);
        Assert.Equal(Fixture.Amount.Sign, Assert.IsAssignableFrom<int>(balanceDict["sign"]));
        Assert.Equal(
            Fixture.Amount.MajorUnit,
            Assert.IsAssignableFrom<BigInteger>(balanceDict["majorUnit"]));
        Assert.Equal(
            Fixture.Amount.MinorUnit,
            Assert.IsAssignableFrom<BigInteger>(balanceDict["minorUnit"]));
        Assert.Equal(Fixture.Amount.GetQuantityString(), balanceDict["quantity"]);
        Assert.Equal(Fixture.Amount.ToString(), balanceDict["string"]);
    }

    [Fact]
    public async Task TotalSupply()
    {
         var legacyToken = Currency.Create("LEG", 0);
        (var source, var blockHash, _) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            totalSupply(
                currency: {{ ticker: ""ABC"", decimalPlaces: 2 }}
                offsetBlockHash: ""{ByteUtil.Hex(blockHash.Bytes)}"") {{
                currency {{ ticker, hash }}
                sign
                majorUnit
                minorUnit
                quantity
                string
            }}
        }}
        ", source: source);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> totalSupplyDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["totalSupply"]);
        IDictionary<string, object> currencyDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(totalSupplyDict["currency"]);
        Assert.Equal("ABC", currencyDict["ticker"]);
        Assert.Equal(ByteUtil.Hex(Fixture.Currency.Hash.Bytes), currencyDict["hash"]);
        FungibleAssetValue expectedTotalSupply
            = Fixture.Amount + Fixture.AdditionalSupply;
        Assert.Equal(
            expectedTotalSupply.Sign,
            Assert.IsAssignableFrom<int>(totalSupplyDict["sign"]));
        Assert.Equal(
            expectedTotalSupply.MajorUnit,
            Assert.IsAssignableFrom<BigInteger>(totalSupplyDict["majorUnit"]));
        Assert.Equal(
            expectedTotalSupply.MinorUnit,
            Assert.IsAssignableFrom<BigInteger>(totalSupplyDict["minorUnit"]));
        Assert.Equal(
            expectedTotalSupply.GetQuantityString(),
            totalSupplyDict["quantity"]);
        Assert.Equal(
            expectedTotalSupply.ToString(),
            totalSupplyDict["string"]);

        result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            totalSupply(
                currency: {{ ticker: ""LEG"", decimalPlaces: 0 }}
                offsetBlockHash: ""{ByteUtil.Hex(blockHash.Bytes)}"") {{
                quantity
            }}
        }}
        ", source: source);
        Assert.Null(result.Errors);
        resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        totalSupplyDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["totalSupply"]);
        Assert.Equal(
            (legacyToken * 0).GetQuantityString(),
            totalSupplyDict["quantity"]);
    }

    [Fact]
    public async Task Validators()
    {
        (var source, var blockHash, _) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            validators(offsetBlockHash: ""{ByteUtil.Hex(blockHash.Bytes)}"") {{
                publicKey
                power
            }}
        }}
        ", source: source);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        object[] validators = Assert.IsAssignableFrom<object[]>(resultDict["validators"]);
        IDictionary<string, object> validatorDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(validators[0]);
        Assert.Equal(Fixture.Validator.PublicKey.ToString("c", null), validatorDict["publicKey"]);
        Assert.Equal(Fixture.Validator.Power, validatorDict["power"]);
    }

    [Fact]
    public async Task ThrowExecutionErrorIfViolateMutualExclusive()
    {
        (var source, var blockHash, var stateRootHash)
            = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            states(
                addresses: [
                    ""{ByteUtil.Hex(Fixture.Address.Bytes)}""
                    ""0x0000000000000000000000000000000000000000""
                ]
                offsetBlockHash: ""{ByteUtil.Hex(blockHash.Bytes)}""
                offsetStateRootHash: ""{ByteUtil.Hex(stateRootHash.Bytes)}"")
        }}
        ", source: source);
        Assert.IsType<ExecutionErrors>(result.Errors);
    }

    [Fact]
    public async Task StatesBySrh()
    {
        (var source, _, var stateRootHash) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            states(
                addresses: [
                    ""{ByteUtil.Hex(Fixture.Address.Bytes)}""
                    ""0x0000000000000000000000000000000000000000""
                ]
                offsetStateRootHash: ""{ByteUtil.Hex(stateRootHash.Bytes)}""
            )
        }}
        ", source: source);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        object[] states =
            Assert.IsAssignableFrom<object[]>(resultDict["states"]);
        Assert.Equal(new[] { _codec.Encode(Fixture.Value), null }, states);
    }

    [Fact]
    public async Task BalanceBySrh()
    {
        (var source, _, var stateRootHash) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            balance(
                owner: ""{ByteUtil.Hex(Fixture.Address.Bytes)}""
                currency: {{ ticker: ""ABC"", decimalPlaces: 2 }}
                offsetStateRootHash: ""{ByteUtil.Hex(stateRootHash.Bytes)}""
            ) {{
                currency {{ ticker, hash }}
                sign
                majorUnit
                minorUnit
                quantity
                string
            }}
        }}
        ", source: source);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> balanceDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["balance"]);
        IDictionary<string, object> currencyDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(balanceDict["currency"]);
        Assert.Equal(Fixture.Currency.Ticker, currencyDict["ticker"]);
        Assert.Equal(ByteUtil.Hex(Fixture.Currency.Hash.Bytes), currencyDict["hash"]);
        Assert.Equal(Fixture.Amount.Sign, Assert.IsAssignableFrom<int>(balanceDict["sign"]));
        Assert.Equal(
            Fixture.Amount.MajorUnit,
            Assert.IsAssignableFrom<BigInteger>(balanceDict["majorUnit"]));
        Assert.Equal(
            Fixture.Amount.MinorUnit,
            Assert.IsAssignableFrom<BigInteger>(balanceDict["minorUnit"]));
        Assert.Equal(Fixture.Amount.GetQuantityString(), balanceDict["quantity"]);
        Assert.Equal(Fixture.Amount.ToString(), balanceDict["string"]);
    }

    [Fact]
    public async Task TotalSupplyBySrh()
    {
         var legacyToken = Currency.Create("LEG", 0);
        (var source, _, var stateRootHash) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            totalSupply(
                currency: {{ ticker: ""ABC"", decimalPlaces: 2 }}
                offsetStateRootHash: ""{ByteUtil.Hex(stateRootHash.Bytes)}""
            ) {{
                currency {{ ticker, hash }}
                sign
                majorUnit
                minorUnit
                quantity
                string
            }}
        }}
        ", source: source);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> totalSupplyDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["totalSupply"]);
        IDictionary<string, object> currencyDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(totalSupplyDict["currency"]);
        Assert.Equal(Fixture.Currency.Ticker, currencyDict["ticker"]);
        Assert.Equal(ByteUtil.Hex(Fixture.Currency.Hash.Bytes), currencyDict["hash"]);
        FungibleAssetValue expectedTotalSupply
            = Fixture.Amount + Fixture.AdditionalSupply;
        Assert.Equal(
            expectedTotalSupply.Sign,
            Assert.IsAssignableFrom<int>(totalSupplyDict["sign"]));
        Assert.Equal(
            expectedTotalSupply.MajorUnit,
            Assert.IsAssignableFrom<BigInteger>(totalSupplyDict["majorUnit"]));
        Assert.Equal(
            expectedTotalSupply.MinorUnit,
            Assert.IsAssignableFrom<BigInteger>(totalSupplyDict["minorUnit"]));
        Assert.Equal(
            expectedTotalSupply.GetQuantityString(),
            totalSupplyDict["quantity"]);
        Assert.Equal(
            expectedTotalSupply.ToString(),
            totalSupplyDict["string"]);

        result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            totalSupply(
                currency: {{ ticker: ""LEG"", decimalPlaces: 0 }}
                offsetStateRootHash: ""{ByteUtil.Hex(stateRootHash.Bytes)}"") {{
                quantity
            }}
        }}
        ", source: source);
        Assert.Null(result.Errors);
        resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        totalSupplyDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["totalSupply"]);
        Assert.Equal(
            (legacyToken * 0).GetQuantityString(),
            totalSupplyDict["quantity"]);
    }

    [Fact]
    public async Task ValidatorsBySrh()
    {
        (var source, _, var stateRootHash) = Fixture.CreateMockBlockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>($@"
        {{
            validators(offsetStateRootHash: ""{ByteUtil.Hex(stateRootHash.Bytes)}"") {{
                publicKey
                power
            }}
        }}
        ", source: source);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        object[] validators = Assert.IsAssignableFrom<object[]>(resultDict["validators"]);
        IDictionary<string, object> validatorDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(validators[0]);
        Assert.Equal(Fixture.Validator.PublicKey.ToString("c", null), validatorDict["publicKey"]);
        Assert.Equal(Fixture.Validator.Power, validatorDict["power"]);
    }
}
