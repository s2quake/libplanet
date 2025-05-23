using System.Runtime.CompilerServices;
using GraphQL.Types;
using Libplanet.Blockchain;
using Libplanet.Explorer.Indexing;
using Libplanet.Explorer.Queries;
using Libplanet.Net;

namespace Libplanet.Explorer.Interfaces
{
    public interface IBlockChainContext
    {
        bool Preloaded { get; }

        BlockChain BlockChain { get; }

        Libplanet.Data.Repository Store { get; }

        Swarm Swarm { get; }

        IBlockChainIndex Index { get; }
    }

    public static class BlockChainContext
    {
        private static readonly ConditionalWeakTable<object, Schema> _schemaObjects =
            new ConditionalWeakTable<object, Schema>();

        public static Schema GetSchema(this IBlockChainContext context)
        {
            return _schemaObjects.GetValue(
                context,
                (_) =>
                {
                    var s = new Schema { Query = new ExplorerQuery(context) };
                    return s;
                });
        }
    }
}
