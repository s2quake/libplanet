using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Execution;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Blockchain.Policies;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Explorer.Queries;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Store.Trie.Nodes;
using Xunit;
using static Libplanet.Explorer.Tests.GraphQLTestUtils;
using System;

namespace Libplanet.Explorer.Tests.Queries;

public partial class StateQueryTest
{
    private static readonly Codec _codec = new Codec();

    [Fact]
    public async Task AccountByBlockHashThenStateAndStates()
    {
        IBlockChainStates source = new MockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>(@"
        {
            account (blockHash: ""01ba4719c80b6fe911b091a7c05124b64eeece964e09c058ef8f9805daca546b"") {
                state (address: ""0x5003712B63baAB98094aD678EA2B24BcE445D076"") {
                    hex
                }
                states (addresses: [""0x5003712B63baAB98094aD678EA2B24BcE445D076"", ""0x0000000000000000000000000000000000000000""]) {
                    hex
                }
            }
        }
        ", source: source);

        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> account =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["account"]);

        IDictionary<string, object> state =
            Assert.IsAssignableFrom<IDictionary<string, object>>(account["state"]);
        Assert.Equal(
            ByteUtil.Hex(_codec.Encode(Null.Value)),
            Assert.IsAssignableFrom<string>(state["hex"]));

        object[] states =
            Assert.IsAssignableFrom<object[]>(account["states"]);
        Assert.Equal(2, states.Length);
        Assert.Equal(
            ByteUtil.Hex(_codec.Encode(Null.Value)),
            Assert.IsAssignableFrom<string>(
                Assert.IsAssignableFrom<IDictionary<string, object>>(states[0])["hex"]));
        Assert.Null(states[1]);
    }

    [Fact]
    public async Task AccountByBlockHashThenBalanceAndBalances()
    {
        IBlockChainStates source = new MockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>(@"
        {
            account (blockHash: ""01ba4719c80b6fe911b091a7c05124b64eeece964e09c058ef8f9805daca546b"") {
                balance (
                    address: ""0x5003712B63baAB98094aD678EA2B24BcE445D076""
                    currencyHash: ""84ba810ca5ac342c122eb7ef455939a8a05d1d40""
                ) {
                    hex
                }
                balances (
                    addresses: [""0x5003712B63baAB98094aD678EA2B24BcE445D076"", ""0x0000000000000000000000000000000000000000""]
                    currencyHash: ""84ba810ca5ac342c122eb7ef455939a8a05d1d40""
                ) {
                    hex
                }
            }
        }
        ", source: source);

        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> account =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["account"]);

        IDictionary<string, object> balance =
            Assert.IsAssignableFrom<IDictionary<string, object>>(account["balance"]);
        Assert.Equal(
            ByteUtil.Hex(_codec.Encode(new Integer(123))),
            Assert.IsAssignableFrom<string>(balance["hex"]));

        object[] balances =
            Assert.IsAssignableFrom<object[]>(account["balances"]);
        Assert.Equal(2, balances.Length);
        Assert.Equal(
            ByteUtil.Hex(_codec.Encode(new Integer(123))),
            Assert.IsAssignableFrom<string>(
                Assert.IsAssignableFrom<IDictionary<string, object>>(balances[0])["hex"]));

        // FIXME: Due to dumb mocking. We need to overhaul mocking.
        Assert.Equal(
            ByteUtil.Hex(_codec.Encode(new Integer(123))),
            Assert.IsAssignableFrom<string>(
                Assert.IsAssignableFrom<IDictionary<string, object>>(balances[1])["hex"]));
    }

    [Fact]
    public async Task AccountByBlockHashThenTotalSupply()
    {
        IBlockChainStates source = new MockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>(@"
        {
            account (blockHash: ""01ba4719c80b6fe911b091a7c05124b64eeece964e09c058ef8f9805daca546b"") {
                totalSupply (
                    currencyHash: ""84ba810ca5ac342c122eb7ef455939a8a05d1d40""
                ) {
                    hex
                }
            }
        }
        ", source: source);

        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> account =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["account"]);

        IDictionary<string, object> totalSupply =
            Assert.IsAssignableFrom<IDictionary<string, object>>(account["totalSupply"]);
        Assert.Equal(
            ByteUtil.Hex(_codec.Encode(new Integer(10000))),
            Assert.IsAssignableFrom<string>(totalSupply["hex"]));
    }

    [Fact]
    public async Task AccountByBlockHashThenValidatorSet()
    {
        IBlockChainStates source = new MockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>(@"
        {
            account (blockHash: ""01ba4719c80b6fe911b091a7c05124b64eeece964e09c058ef8f9805daca546b"") {
                validatorSet {
                    hex
                }
            }
        }
        ", source: source);

        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> account =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["account"]);

        IDictionary<string, object> totalSupply =
            Assert.IsAssignableFrom<IDictionary<string, object>>(account["validatorSet"]);
        Assert.Equal(
            ByteUtil.Hex(_codec.Encode(new ValidatorSet(new List<Validator>
                {
                    new(
                        PublicKey.FromHex(
                            "032038e153d344773986c039ba5dbff12ae70cfdf6ea8beb7c5ea9b361a72a9233"),
                        new BigInteger(1)),
                }).Bencoded)),
            Assert.IsAssignableFrom<string>(totalSupply["hex"]));
    }

    [Fact]
    public async Task AccountByStateRootHashThenStateAndStates()
    {
        IBlockChainStates source = new MockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>(@"
        {
            account (stateRootHash: ""c33b27773104f75ac9df5b0533854108bd498fab31e5236b6f1e1f6404d5ef64"") {
                state (address: ""0x5003712B63baAB98094aD678EA2B24BcE445D076"") {
                    hex
                }
                states (addresses: [""0x5003712B63baAB98094aD678EA2B24BcE445D076"", ""0x0000000000000000000000000000000000000000""]) {
                    hex
                }
            }
        }
        ", source: source);

        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> account =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["account"]);

        IDictionary<string, object> state =
            Assert.IsAssignableFrom<IDictionary<string, object>>(account["state"]);
        Assert.Equal(
            ByteUtil.Hex(_codec.Encode(Null.Value)),
            Assert.IsAssignableFrom<string>(state["hex"]));

        object[] states =
            Assert.IsAssignableFrom<object[]>(account["states"]);
        Assert.Equal(2, states.Length);
        Assert.Equal(
            ByteUtil.Hex(_codec.Encode(Null.Value)),
            Assert.IsAssignableFrom<string>(
                Assert.IsAssignableFrom<IDictionary<string, object>>(states[0])["hex"]));
        Assert.Null(states[1]);
    }

    // FIXME: We need proper mocks to test more complex scenarios.
    [Fact]
    public async Task AccountsByBlockHashesThenStateAndStates()
    {
        IBlockChainStates source = new MockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>(@"
        {
            accounts (blockHashes: [""01ba4719c80b6fe911b091a7c05124b64eeece964e09c058ef8f9805daca546b""]) {
                state (address: ""0x5003712B63baAB98094aD678EA2B24BcE445D076"") {
                    hex
                }
                states (addresses: [""0x5003712B63baAB98094aD678EA2B24BcE445D076"", ""0x0000000000000000000000000000000000000000""]) {
                    hex
                }
            }
        }
        ", source: source);

        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        object[] accounts =
            Assert.IsAssignableFrom<object[]>(resultDict["accounts"]);

        IDictionary<string,object> account =
            Assert.IsAssignableFrom<IDictionary<string, object>>(Assert.Single(accounts));
        IDictionary<string, object> state =
            Assert.IsAssignableFrom<IDictionary<string, object>>(account["state"]);
        Assert.Equal(
            ByteUtil.Hex(_codec.Encode(Null.Value)),
            Assert.IsAssignableFrom<string>(state["hex"]));

        object[] states =
            Assert.IsAssignableFrom<object[]>(account["states"]);
        Assert.Equal(2, states.Length);
        Assert.Equal(
            ByteUtil.Hex(_codec.Encode(Null.Value)),
            Assert.IsAssignableFrom<string>(
                Assert.IsAssignableFrom<IDictionary<string, object>>(states[0])["hex"]));
        Assert.Null(states[1]);
    }

    // FIXME: We need proper mocks to test more complex scenarios.
    [Fact]
    public async Task AccountsByStateRootHashesThenStateAndStates()
    {
        IBlockChainStates source = new MockChainStates();
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>(@"
        {
            accounts (stateRootHashes: [""c33b27773104f75ac9df5b0533854108bd498fab31e5236b6f1e1f6404d5ef64""]) {
                state (address: ""0x5003712B63baAB98094aD678EA2B24BcE445D076"") {
                    hex
                }
                states (addresses: [""0x5003712B63baAB98094aD678EA2B24BcE445D076"", ""0x0000000000000000000000000000000000000000""]) {
                    hex
                }
            }
        }
        ", source: source);

        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        object[] accounts =
            Assert.IsAssignableFrom<object[]>(resultDict["accounts"]);

        IDictionary<string,object> account =
            Assert.IsAssignableFrom<IDictionary<string, object>>(Assert.Single(accounts));
        IDictionary<string, object> state =
            Assert.IsAssignableFrom<IDictionary<string, object>>(account["state"]);
        Assert.Equal(
            ByteUtil.Hex(_codec.Encode(Null.Value)),
            Assert.IsAssignableFrom<string>(state["hex"]));

        object[] states =
            Assert.IsAssignableFrom<object[]>(account["states"]);
        Assert.Equal(2, states.Length);
        Assert.Equal(
            ByteUtil.Hex(_codec.Encode(Null.Value)),
            Assert.IsAssignableFrom<string>(
                Assert.IsAssignableFrom<IDictionary<string, object>>(states[0])["hex"]));
        Assert.Null(states[1]);
    }

    [Fact]
    public async Task WorldStates()
    {
        (IBlockChainStates, IBlockPolicy) source = (
            new MockChainStates(), new BlockPolicy()
        );
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>(@"
        {
            worldState(
                 offsetBlockHash:
                     ""01ba4719c80b6fe911b091a7c05124b64eeece964e09c058ef8f9805daca546b""
            )
            {
                stateRootHash
                legacy
            }
        }
        ", source: source);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> states =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["worldState"]);
        Assert.NotNull(states["stateRootHash"]);
        Assert.True((bool)states["legacy"]);
    }

    [Fact]
    public async Task AccountStates()
    {
        (IBlockChainStates, IBlockPolicy) source = (
            new MockChainStates(), new BlockPolicy()
        );
        ExecutionResult result = await ExecuteQueryAsync<StateQuery>(@"
        {
            accountState(
                 accountAddress: ""0x40837BFebC1b192600023a431400557EA5FDE51a"",
                 offsetBlockHash:
                     ""01ba4719c80b6fe911b091a7c05124b64eeece964e09c058ef8f9805daca546b""
            )
            {
                stateRootHash
            }
        }
        ", source: source);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        IDictionary<string, object> states =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["accountState"]);
        Assert.NotNull(states["stateRootHash"]);
    }
}
