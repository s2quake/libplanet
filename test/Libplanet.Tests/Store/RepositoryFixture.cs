using System.Security.Cryptography;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Data;
using Libplanet.Types;
using Libplanet.State.Builtin;

namespace Libplanet.Tests.Store;

public abstract class RepositoryFixture : IDisposable
{
    private bool disposedValue;

    protected RepositoryFixture(Repository repository, BlockchainOptions options)
    {
        Address1 = new Address(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
        ]);
        Address2 = new Address(
        [
            0x55, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xdd,
        ]);
        Address3 = new Address(
        [
            0xa3, 0x4b, 0x0c, 0x91, 0xda, 0x58, 0xd4, 0x73, 0xd3, 0x70,
            0xc4, 0x5b, 0xf9, 0x6f, 0x6d, 0x98, 0xa5, 0x01, 0xd9, 0x22,
        ]);
        Address4 = new Address(
        [
            0xbf, 0x78, 0x67, 0x29, 0xba, 0x04, 0x1b, 0xa7, 0x6f, 0xfb,
            0xa0, 0x6c, 0x8c, 0x4d, 0xc1, 0x24, 0xee, 0x3e, 0x8c, 0x8b,
        ]);
        Address5 = new Address(
        [
            0x03, 0xf0, 0x42, 0x7f, 0x2e, 0x6c, 0x0f, 0x5f, 0xdb, 0xd3,
            0x77, 0x9d, 0xb2, 0x84, 0xd6, 0x1b, 0x04, 0x38, 0xdf, 0xb6,
        ]);
        TxId1 = new TxId(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x9c, 0xcc,
        ]);
        TxId2 = new TxId(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x9c, 0xdd,
        ]);
        TxId3 = new TxId(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x9c, 0xee,
        ]);
        Hash1 = new BlockHash(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x9c, 0xcc,
        ]);
        Hash2 = new BlockHash(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x9c, 0xdd,
        ]);
        Hash3 = new BlockHash(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x9c, 0xee,
        ]);

        Options = options;
        Repository = repository;
        BlockExecutor = new BlockExecutor(repository.States, Options.SystemActions);
        Proposer = TestUtils.GenesisProposer;
        ProposerPower = TestUtils.Validators[0].Power;
        GenesisBlock = new BlockBuilder
        {
            Transactions =
            [
                new TransactionBuilder
                {
                    Actions = [new Initialize { Validators = TestUtils.Validators }]
                }.Create(Proposer),
            ],
        }.Create(Proposer);
        GenesisBlockExecutionInfo = BlockExecutor.Execute((RawBlock)GenesisBlock);
        GenesisStateRootHash = GenesisBlockExecutionInfo.StateRootHash;
        Block1 = TestUtils.ProposeNextBlock(
            GenesisBlock,
            proposer: Proposer,
            previousStateRootHash: GenesisStateRootHash,
            lastCommit: null);
        Block2 = TestUtils.ProposeNextBlock(
            Block1,
            proposer: Proposer,
            previousStateRootHash: GenesisStateRootHash,
            lastCommit: TestUtils.CreateBlockCommit(Block1));
        Block3 = TestUtils.ProposeNextBlock(
            Block2,
            proposer: Proposer,
            previousStateRootHash: GenesisStateRootHash,
            lastCommit: TestUtils.CreateBlockCommit(Block2));
        Block3Alt = TestUtils.ProposeNextBlock(
            Block2, proposer: Proposer, previousStateRootHash: GenesisStateRootHash);
        Block4 = TestUtils.ProposeNextBlock(
            Block3, proposer: Proposer, previousStateRootHash: GenesisStateRootHash);
        Block5 = TestUtils.ProposeNextBlock(
            Block4, proposer: Proposer, previousStateRootHash: GenesisStateRootHash);

        Transaction1 = MakeTransaction();
        Transaction2 = MakeTransaction();
        Transaction3 = MakeTransaction();
    }

    public string Path { get; set; } = string.Empty;

    public string Scheme { get; set; } = string.Empty;

    public Guid Id { get; } = Guid.NewGuid();

    public Address Address1 { get; }

    public Address Address2 { get; }

    public Address Address3 { get; }

    public Address Address4 { get; }

    public Address Address5 { get; }

    public TxId TxId1 { get; }

    public TxId TxId2 { get; }

    public TxId TxId3 { get; }

    public BlockHash Hash1 { get; }

    public BlockHash Hash2 { get; }

    public BlockHash Hash3 { get; }

    public PrivateKey Proposer { get; }

    public BigInteger ProposerPower { get; }

    public Block GenesisBlock { get; }

    public HashDigest<SHA256> GenesisStateRootHash { get; }

    public Block Block1 { get; }

    public Block Block2 { get; }

    public Block Block3 { get; }

    public Block Block3Alt { get; }

    public Block Block4 { get; }

    public Block Block5 { get; }

    public Transaction Transaction1 { get; }

    public Transaction Transaction2 { get; }

    public Transaction Transaction3 { get; }

    public Repository Repository { get; }

    public BlockchainOptions Options { get; }

    public BlockExecutor BlockExecutor { get; }

    public BlockExecutionInfo GenesisBlockExecutionInfo { get; }

    public Transaction MakeTransaction(
        IEnumerable<DumbAction>? actions = null,
        long nonce = 0,
        PrivateKey? privateKey = null,
        DateTimeOffset? timestamp = null)
    {
        privateKey ??= new PrivateKey();
        actions ??= [];

        return new TransactionMetadata
        {
            Nonce = nonce,
            Signer = privateKey.Address,
            GenesisHash = GenesisBlock.BlockHash,
            Actions = actions.ToBytecodes(),
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
        }.Sign(privateKey);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (Repository is IDisposable disposableRepository)
            {
                disposableRepository.Dispose();
            }

            disposedValue = true;
        }
    }
}
