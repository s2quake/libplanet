using GraphQL;
using GraphQL.Types;
using Libplanet.Store.DataStructures;

namespace Libplanet.Explorer.GraphTypes
{
    public class TrieType : ObjectGraphType<ITrie>
    {
        public TrieType()
        {
            Name = "Trie";

            Field<IValueType>(
                name: "value",
                description: "Gets the value stored at given key.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<KeyBytesType>>
                    {
                        Name = "key",
                        Description = "The key to search.",
                    }),
                resolve: context => context.Source[context.GetArgument<string>("key")]);

            Field<IValueType>(
                name: "values",
                description: "Gets the values stored at given multiple keys.",
                arguments: new QueryArguments(
                    new QueryArgument<
                        NonNullGraphType<ListGraphType<NonNullGraphType<KeyBytesType>>>>
                    {
                        Name = "keys",
                        Description = "The list of keys to search.",
                    }),
                resolve: context => context
                    .GetArgument<string[]>("keys")
                    .Select(key => context.Source[key])
                    .ToArray());
        }
    }
}
