using System.Security.Cryptography;
using GraphQL;
using GraphQL.Types;
using Libplanet.Action.State;
using Libplanet.Common;
using Libplanet.Explorer.GraphTypes;

namespace Libplanet.Explorer.Queries;

public class RawStateQuery : ObjectGraphType<IBlockChainStates>
{
    public RawStateQuery()
    {
        Name = "RawStateQuery";

        // FIXME: IBlockChainStates does not support direct retrieval of an ITrie.
        Field<NonNullGraphType<TrieType>>(
            "trie",
            description: "Retrieves trie from given state root hash.",
            arguments: new QueryArguments(
                new QueryArgument<HashDigestSHA256Type> { Name = "stateRootHash" }
            ),
            resolve: context => context.Source.GetWorld(
                context.GetArgument<HashDigest<SHA256>>("stateRootHash")).Trie
        );
    }
}
