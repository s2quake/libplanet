using System.Threading.Tasks;
using Libplanet.Action;
using Libplanet.Explorer.Queries;
using Xunit;

namespace Libplanet.Explorer.Tests.Queries;

public class TransactionQueryGeneratedWithIndexTest : TransactionQueryGeneratedTest
{
    public TransactionQueryGeneratedWithIndexTest()
    {
        Source = new MockBlockChainContextWithIndex(Fx.Chain);
        QueryGraph = new TransactionQuery(Source);
    }

    [SkippableFact(Skip = "transactionQuery.transactions does not support indexing.")]
    public override Task Transactions() => Task.CompletedTask;

    protected override MockBlockChainContext Source { get; }

    protected override TransactionQuery QueryGraph { get; }
}
