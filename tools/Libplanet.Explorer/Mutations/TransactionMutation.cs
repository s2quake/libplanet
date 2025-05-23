using GraphQL;
using GraphQL.Types;
using Libplanet.Blockchain;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Explorer.Interfaces;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types.Transactions;

namespace Libplanet.Explorer.Mutations
{
    public class TransactionMutation : ObjectGraphType
    {
        private readonly IBlockChainContext _context;

        public TransactionMutation(IBlockChainContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            Field<TransactionType>(
                "stage",
                description: "Stage transaction to current chain",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "payload",
                        #pragma warning disable MEN002
                        Description = "The hexadecimal string of the serialized transaction to stage.",
                        #pragma warning restore MEN002
                    }),
                resolve: context =>
                {
                    BlockChain chain = _context.BlockChain;
                    byte[] payload = ByteUtility.ParseHex(context.GetArgument<string>("payload"));
                    Transaction tx = ModelSerializer.DeserializeFromBytes<Transaction>(payload);
                    if (!chain.StagedTransactions.TryAdd(tx))
                    {
                        throw new ExecutionError(
                            "Failed to stage given tx; it may be already expired or ignored.");
                    }

                    return tx;
                });
        }
    }
}
