using System.Security.Cryptography;
using GraphQL;
using GraphQL.Types;
using Libplanet.Blockchain;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Types;

namespace Libplanet.Explorer.Queries;

public class RawStateQuery : ObjectGraphType<BlockChainStates>
{
    public RawStateQuery()
    {
        Name = "RawStateQuery";

        // FIXME: IBlockChainStates does not support direct retrieval of an ITrie.
        Field<NonNullGraphType<TrieType>>(
            "trie",
            description: "Retrieves trie from given state root hash.",
            arguments: new QueryArguments(
                new QueryArgument<HashDigestSHA256Type> { Name = "stateRootHash" }),
            resolve: context => context.Source.GetWorld(
                context.GetArgument<HashDigest<SHA256>>("stateRootHash")).Trie);
    }
}
