using System.Threading.Tasks;
using GraphQL;
using GraphQL.Execution;
using Libplanet.Action;
using Libplanet.Action.Sys;
using Libplanet.Blockchain;
using Libplanet.Explorer.Queries;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;
using static Libplanet.Explorer.Tests.GraphQLTestUtils;

namespace Libplanet.Explorer.Tests.Queries;

public class TransactionQueryTest
{
    protected readonly BlockChain Chain;
    protected MockBlockChainContext Source;
    protected TransactionQuery QueryGraph;

    public TransactionQueryTest()
    {
        Chain = Libplanet.Tests.TestUtils.MakeBlockChain(
            new BlockChainOptions(),
            privateKey: new PrivateKey(),
            timestamp: DateTimeOffset.UtcNow);
        Source = new MockBlockChainContext(Chain);
        QueryGraph = new TransactionQuery(Source);
    }

    [Fact]
    public async Task BindSignatureWithCustomActions()
    {
        var privateKey = new PrivateKey();
        var tx = new TransactionMetadata
        {
            Nonce = 0L,
            Signer = privateKey.Address,
            GenesisHash = Source.BlockChain.Genesis.BlockHash,
            Actions = Array.Empty<NullAction>().ToBytecodes(),
        }.Sign(privateKey);
        // tx.UnsignedTx.MarshalUnsignedTx();
        ExecutionResult result = await ExecuteQueryAsync(@$"
        {{
            bindSignature(
                metadata: ""{ByteUtility.Hex(ModelSerializer.SerializeToBytes(tx.Metadata))}"",
                signature: ""{ByteUtility.Hex(tx.Signature)}""
            )
         }}
         ", QueryGraph, source: Source);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        Assert.Equal(
            ModelSerializer.SerializeToBytes(tx),
            ByteUtility.ParseHex((string)resultDict["bindSignature"]));
    }

    [Fact]
    public async Task BindSignatureWithSystemAction()
    {
        var foo = Currency.Create("FOO", 2);
        var action = new Initialize
        {
            Validators = [Validator.Create(new PrivateKey().Address, 1)],
            States = new Dictionary<Address, object>
            {
                [default] = "initial value"
            }.ToImmutableDictionary(),
        };
        var txKey = new PrivateKey();
        var tx = new TransactionMetadata
        {
            Nonce = 0L,
            Signer = txKey.Address,
            GenesisHash = Source.BlockChain.Genesis.BlockHash,
            Actions = new IAction[] { action }.ToBytecodes(),
        }.Sign(txKey);
        ExecutionResult result = await ExecuteQueryAsync(@$"
        {{
            bindSignature(
                metadata: ""{ByteUtility.Hex(ModelSerializer.SerializeToBytes(tx.Metadata))}"",
                signature: ""{ByteUtility.Hex(tx.Signature)}""
            )
         }}
         ", QueryGraph, source: Source);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        Assert.Equal(
            ModelSerializer.SerializeToBytes(tx),
            ByteUtility.ParseHex((string)resultDict["bindSignature"]));
    }

    [Fact]
    public async Task NextNonce()
    {
        async Task AssertNextNonce(long expected, Address address)
        {
            var result = await ExecuteQueryAsync(@$"
            {{
                nextNonce(
                    address: ""{address}""
                )
            }}
            ", QueryGraph, source: Source);

            Assert.Null(result.Errors);
            ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
            IDictionary<string, object> resultDict =
                Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
            Assert.Equal(Source.BlockChain.GetNextTxNonce(address), (long)resultDict["nextNonce"]);
            Assert.Equal(expected, (long)resultDict["nextNonce"]);
        }

        var key1 = new PrivateKey();
        // account nonce is 0 in the beginning
        await AssertNextNonce(0, key1.Address);

        // staged txs increase next nonce
        Source.BlockChain.MakeTransaction(key1, [new NullAction()]);
        await AssertNextNonce(1, key1.Address);
        Source.BlockChain.MakeTransaction(key1, [new NullAction()]);
        await AssertNextNonce(2, key1.Address);
        var block = Source.BlockChain.ProposeBlock(new PrivateKey());
        Source.BlockChain.Append(block, Libplanet.Tests.TestUtils.CreateBlockCommit(block));
        await AssertNextNonce(2, key1.Address);

        var key2 = new PrivateKey();
        await AssertNextNonce(0, key2.Address);

        // staging txs of key2 does not increase nonce of key1
        Source.BlockChain.MakeTransaction(key2, [new NullAction()]);
        block = Source.BlockChain.ProposeBlock(
            new PrivateKey(),
            Libplanet.Tests.TestUtils.CreateBlockCommit(block));
        Source.BlockChain.Append(block, Libplanet.Tests.TestUtils.CreateBlockCommit(block));
        await AssertNextNonce(1, key2.Address);
        await AssertNextNonce(2, key1.Address);

        // unstaging txs decrease nonce
        Source.BlockChain.MakeTransaction(key1, [new NullAction()]);
        await AssertNextNonce(3, key1.Address);
        Source.BlockChain.MakeTransaction(key1, [new NullAction()]);
        await AssertNextNonce(4, key1.Address);
        Source.BlockChain.GetStagedTransactionIds()
            .Select(item => Source.BlockChain.Transactions[item])
            .Select(Source.BlockChain.UnstageTransaction)
            .ToImmutableList();
        await AssertNextNonce(2, key1.Address);
    }
}
