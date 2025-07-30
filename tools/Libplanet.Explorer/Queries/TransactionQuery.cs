using System.Text;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet.Blockchain;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Explorer.Interfaces;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Explorer.Queries
{
    public class TransactionQuery : ObjectGraphType
    {
        private static readonly Codec _codec = new Codec();
        private readonly IBlockChainContext _context;

        // FIXME should be refactored to reduce LoC of constructor.
        #pragma warning disable MEN003
        public TransactionQuery(IBlockChainContext context)
        #pragma warning restore MEN003
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<TransactionType>>>>(
                "transactions",
                arguments: new QueryArguments(
                    new QueryArgument<AddressType>
                    {
                        Name = "signer",
                        DefaultValue = null,
                    },
                    new QueryArgument<AddressType>
                    {
                        Name = "involvedAddress",
                        DefaultValue = null,
                    },
                    new QueryArgument<BooleanGraphType>
                    {
                        Name = "desc",
                        DefaultValue = false,
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "offset",
                        DefaultValue = 0,
                    },
                    new QueryArgument<IntGraphType> { Name = "limit" }
                ),
                resolve: context =>
                {
                    var signer = context.GetArgument<Address?>("signer");
                    bool desc = context.GetArgument<bool>("desc");
                    long offset = context.GetArgument<long>("offset");
                    int? limit = context.GetArgument<int?>("limit");

                    return ExplorerQuery.ListTransactions(signer, desc, offset, limit);
                }
            );

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<TransactionType>>>>(
                "stagedTransactions",
                arguments: new QueryArguments(
                    new QueryArgument<AddressType>
                    {
                        Name = "signer",
                        DefaultValue = null,
                    },
                    new QueryArgument<AddressType>
                    {
                        Name = "involvedAddress",
                        DefaultValue = null,
                    },
                    new QueryArgument<BooleanGraphType>
                    {
                        Name = "desc",
                        DefaultValue = false,
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "offset",
                        DefaultValue = 0,
                    },
                    new QueryArgument<IntGraphType> { Name = "limit" }
                ),
                resolve: context =>
                {
                    var signer = context.GetArgument<Address?>("signer");
                    bool desc = context.GetArgument<bool>("desc");
                    int offset = context.GetArgument<int>("offset");
                    int? limit = context.GetArgument<int?>("limit", null);

                    return ExplorerQuery.ListStagedTransactions(
                        signer,
                        desc,
                        offset,
                        limit
                    );
                }
            );

            Field<TransactionType>(
                "transaction",
                arguments: new QueryArguments(
                    new QueryArgument<IdGraphType> { Name = "id" }
                ),
                resolve: context => ExplorerQuery.GetTransaction(
                    new TxId(ByteUtil.ParseHex(context.GetArgument<string>("id")
                        ?? throw new ArgumentException("Given id cannot be null."))))
            );

            Field<NonNullGraphType<ByteStringType>>(
                name: "unsignedTransaction",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "publicKey",
                        Description = "The hexadecimal string of public key for Transaction.",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "plainValue",
                        Description = "The hexadecimal string of plain value for Action.",
                    },
                    new QueryArgument<LongGraphType>
                    {
                        Name = "nonce",
                        Description = "The nonce for Transaction.",
                    }
                ),
                resolve: context =>
                {
                    BlockChain chain = _context.BlockChain;
                    string plainValueString = context.GetArgument<string>("plainValue");
                    IValue plainValue = _codec.Decode(ByteUtil.ParseHex(plainValueString));
                    var publicKey = PublicKey.Parse(context.GetArgument<string>("publicKey"));
                    Address signer = publicKey.Address;
                    long nonce = context.GetArgument<long?>("nonce") ??
                        chain.GetNextTxNonce(signer);
                    var signingMetadata = TxSigningMetadata.Create(publicKey, nonce);
                    var invoice = new TxInvoice
                    {
                        GenesisHash = chain.Genesis.Hash,
                        Actions = [plainValue],
                    };
                    var unsignedTx = new UnsignedTx
                    {
                        Invoice = invoice,
                        SigningMetadata = signingMetadata,
                    };
                    return unsignedTx.SerializeUnsignedTx();
                }
            );

            Field<NonNullGraphType<LongGraphType>>(
                name: "nextNonce",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "address",
                        Description = "Address of the account to get the next tx nonce.",
                    }
                ),
                resolve: context =>
                    _context.BlockChain.GetNextTxNonce(context.GetArgument<Address>("address"))
            );

            Field<NonNullGraphType<StringGraphType>>(
                name: "bindSignature",
                #pragma warning disable MEN002
                description: "Attach the given signature to the given transaction and return tx as hexadecimal",
                #pragma warning restore MEN002
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "unsignedTransaction",
                        #pragma warning disable MEN002
                        Description = "The hexadecimal string of unsigned transaction to attach the given signature.",
                        #pragma warning restore MEN002
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "signature",
                        #pragma warning disable MEN002
                        Description = "The hexadecimal string of the given unsigned transaction.",
                        #pragma warning restore MEN002
                    }
                ),
                resolve: context =>
                {
                    ImmutableArray<byte> signature = ByteUtil.ParseHexToImmutable(
                        context.GetArgument<string>("signature")
                    );
                    var unsignedTx = TxMarshaler.DeserializeUnsignedTx(
                        Encoding.UTF8.GetString(
                            ByteUtil.ParseHex(context.GetArgument<string>("unsignedTransaction")))
                    );
                    var signedTransaction = unsignedTx.Verify(signature);
                    return ByteUtil.Hex(ModelSerializer.SerializeToBytes(signedTransaction));
                }
            );

            Field<NonNullGraphType<TxResultType>>(
                name: "transactionResult",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>>
                    {
                        Name = "txId",
                        Description = "transaction id.",
                    }
                ),
                resolve: context =>
                {
                    var blockChain = _context.BlockChain;
                    var store = _context.Store;
                    var index = _context.Index;
                    var txId = new TxId(ByteUtil.ParseHex(context.GetArgument<string>("txId")));

                    if (GetBlockContainingTx(_context, txId) is { } block)
                    {
                        return _context.BlockChain.GetTxExecution(block.Hash, txId) is { } execution
                            ? new TxResult
                            {
                                TxStatus = execution.Fail ? TxStatus.FAILURE : TxStatus.SUCCESS,
                                BlockHeight = block.Height,
                                BlockHash = block.Hash.ToString(),
                                InputState = execution.InputState,
                                OutputState = execution.OutputState,
                                ExceptionNames = execution.ExceptionNames,
                            }
                            : new TxResult
                            {
                                TxStatus = TxStatus.INCLUDED,
                                BlockHeight = block.Height,
                                BlockHash = block.Hash.ToString(),
                            };
                    }
                    else
                    {
                        return blockChain.GetStagedTransactionIds().Contains(txId)
                            ? new TxResult
                            {
                                TxStatus = TxStatus.STAGING,
                            }
                            : new TxResult
                            {
                                TxStatus = TxStatus.INVALID,
                            };
                    }
                }
            );

            Name = "TransactionQuery";
        }

        /// <summary>
        /// Gets the <see cref="Block"/> from the context <see cref="BlockChain"/> containing
        /// given <paramref name="txId"/>.
        /// </summary>
        /// <param name="context">The <see cref="IBlockChainContext"/> to use as context.</param>
        /// <param name="txId">The target <see cref="TxId"/> that a <see cref="Block"/>
        /// must contain.</param>
        /// <returns>The <see cref="Block"/> containing <paramref name="txId"/> if found,
        /// otherwise <see langword="null"/>.</returns>
        private static Block? GetBlockContainingTx(IBlockChainContext context, TxId txId)
        {
            // Try searching index first.
            if (context.Index is { } index)
            {
                if (index.TryGetContainedBlockHashById(txId, out var blockHash))
                {
                    return context.BlockChain[blockHash];
                }
            }

            // If not found in index, search IStore directly.
            var blockHashCandidates = context.Store.IterateTxIdBlockHashIndex(txId);
            foreach (var blockHashCandidate in blockHashCandidates)
            {
                if (context.BlockChain.ContainsBlock(blockHashCandidate))
                {
                    return context.BlockChain[blockHashCandidate];
                }
            }

            return null;
        }
    }
}
