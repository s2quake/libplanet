using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.TestUtilities;
using Libplanet.Types;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.Tests.Action;

public sealed class WorldTest
{
    private readonly ISigner[] _signers;
    private readonly Address[] _addresses;
    private readonly Currency[] _currencies;
    private readonly World _initWorld;
    private readonly ITestOutputHelper _output;

    public WorldTest(ITestOutputHelper output)
    {
        _output = output;
        _signers =
        [
            Currencies.MinterA,
            Currencies.MinterB,
            new PrivateKey().AsSigner(),
            new PrivateKey().AsSigner(),
        ];

        _addresses = [.. _signers.Select(signer => signer.Address)];

        _currencies =
        [
            Currencies.CurrencyA,
            Currencies.CurrencyB,
            Currencies.CurrencyC,
            Currencies.CurrencyD,
            Currencies.CurrencyE,
            Currencies.CurrencyF,
        ];

        World initMockWorldState = new World() with { Signer = _addresses[0] };
        _initWorld = initMockWorldState
            .SetBalance(_addresses[0], _currencies[0], 5)
            .SetBalance(_addresses[0], _currencies[2], 10)
            .SetBalance(_addresses[0], _currencies[4], 5)
            .SetBalance(_addresses[1], _currencies[2], 15)
            .SetBalance(_addresses[1], _currencies[3], 20)
            .SetValidators([.. _signers.Select(key => new Validator { Address = key.Address })]);
    }

    public int ProtocolVersion { get; }

    public IActionContext CreateContext(Address signer) => new ActionContext
    {
        Signer = signer,
        Proposer = signer,
        BlockProtocolVersion = ProtocolVersion,
    };

    [Fact]
    public void InitialSetup()
    {
        // All non-zero balances.
        Assert.Equal(Value(0, 5), _initWorld.GetBalance(_addresses[0], _currencies[0]));
        Assert.Equal(Value(2, 10), _initWorld.GetBalance(_addresses[0], _currencies[2]));
        Assert.Equal(Value(4, 5), _initWorld.GetBalance(_addresses[0], _currencies[4]));
        Assert.Equal(Value(2, 15), _initWorld.GetBalance(_addresses[1], _currencies[2]));
        Assert.Equal(Value(3, 20), _initWorld.GetBalance(_addresses[1], _currencies[3]));

        // Exhaustive check for the rest.
        Assert.Equal(Value(1, 0), _initWorld.GetBalance(_addresses[0], _currencies[1]));
        Assert.Equal(Value(3, 0), _initWorld.GetBalance(_addresses[0], _currencies[3]));
        Assert.Equal(Value(5, 0), _initWorld.GetBalance(_addresses[0], _currencies[5]));

        Assert.Equal(Value(0, 0), _initWorld.GetBalance(_addresses[1], _currencies[0]));
        Assert.Equal(Value(1, 0), _initWorld.GetBalance(_addresses[1], _currencies[1]));
        Assert.Equal(Value(4, 0), _initWorld.GetBalance(_addresses[1], _currencies[4]));
        Assert.Equal(Value(5, 0), _initWorld.GetBalance(_addresses[1], _currencies[5]));

        Assert.Equal(Value(0, 0), _initWorld.GetBalance(_addresses[2], _currencies[0]));
        Assert.Equal(Value(1, 0), _initWorld.GetBalance(_addresses[2], _currencies[1]));
        Assert.Equal(Value(2, 0), _initWorld.GetBalance(_addresses[2], _currencies[2]));
        Assert.Equal(Value(3, 0), _initWorld.GetBalance(_addresses[2], _currencies[3]));
        Assert.Equal(Value(4, 0), _initWorld.GetBalance(_addresses[2], _currencies[4]));
        Assert.Equal(Value(5, 0), _initWorld.GetBalance(_addresses[2], _currencies[5]));

        Assert.Equal(Value(0, 0), _initWorld.GetBalance(_addresses[3], _currencies[0]));
        Assert.Equal(Value(1, 0), _initWorld.GetBalance(_addresses[3], _currencies[1]));
        Assert.Equal(Value(2, 0), _initWorld.GetBalance(_addresses[3], _currencies[2]));
        Assert.Equal(Value(3, 0), _initWorld.GetBalance(_addresses[3], _currencies[3]));
        Assert.Equal(Value(4, 0), _initWorld.GetBalance(_addresses[3], _currencies[4]));
        Assert.Equal(Value(5, 0), _initWorld.GetBalance(_addresses[3], _currencies[5]));
    }

    [Fact]
    public void TransferAsset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _initWorld.TransferAsset(_addresses[0], _addresses[1], Value(0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _initWorld.TransferAsset(_addresses[0], _addresses[1], Value(0, -1)));
        Assert.Throws<InsufficientBalanceException>(() =>
            _initWorld.TransferAsset(_addresses[0], _addresses[1], Value(0, 6)));

        World world = _initWorld.TransferAsset(_addresses[0], _addresses[1], Value(0, 4));
        Assert.Equal(Value(0, 1), world.GetBalance(_addresses[0], _currencies[0]));
        Assert.Equal(Value(0, 4), world.GetBalance(_addresses[1], _currencies[0]));

        world = _initWorld.TransferAsset(_addresses[0], _addresses[0], Value(0, 2));
        Assert.Equal(Value(0, 5), world.GetBalance(_addresses[0], _currencies[0]));
    }

    [Fact]
    public void TransferAssetInBlock()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        // Mint
        var action = DumbAction.Create(null, (null, _addresses[1], 20));
        var tx = new TransactionMetadata
        {
            Signer = _signers[0].Address,
            GenesisBlockHash = blockchain.Genesis.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Actions = new[] { action }.ToBytecodes(),
        }.Sign(_signers[0]);
        var block1 = new BlockBuilder
        {
            Height = blockchain.Tip.Height + 1,
            PreviousBlockHash = blockchain.Tip.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
            Transactions = [tx],
        }.Create(proposer);
        var blockCommit1 = TestUtils.CreateBlockCommit(block1);
        blockchain.Append(block1, blockCommit1);
        Assert.Equal(
            DumbAction.DumbCurrency * 0,
            blockchain
                .GetWorld()
                .GetBalance(_addresses[0], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 20,
            blockchain
                .GetWorld()
                .GetBalance(_addresses[1], DumbAction.DumbCurrency));

        // Transfer
        action = DumbAction.Create(null, (_addresses[1], _addresses[0], 5));
        tx = new TransactionMetadata
        {
            Nonce = 1,
            Signer = _signers[0].Address,
            Timestamp = DateTimeOffset.UtcNow,
            GenesisBlockHash = blockchain.Genesis.BlockHash,
            Actions = new[] { action }.ToBytecodes(),
        }.Sign(_signers[0]);
        var block2 = new BlockBuilder
        {
            Height = blockchain.Tip.Height + 1,
            PreviousBlockHash = blockchain.Tip.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
            Transactions = [tx],
        }.Create(proposer);
        var blockCommit2 = TestUtils.CreateBlockCommit(block2);
        blockchain.Append(block2, blockCommit2);
        Assert.Equal(
            DumbAction.DumbCurrency * 5,
            blockchain
                .GetWorld()
                .GetBalance(_addresses[0], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 15,
            blockchain
                .GetWorld()
                .GetBalance(_addresses[1], DumbAction.DumbCurrency));

        // Transfer bugged
        action = DumbAction.Create((_addresses[0], "a"), (_addresses[0], _addresses[0], 1));
        tx = new TransactionMetadata
        {
            Nonce = blockchain.GetNextTxNonce(_addresses[0]),
            Signer = _signers[0].Address,
            Timestamp = DateTimeOffset.UtcNow,
            GenesisBlockHash = blockchain.Genesis.BlockHash,
            Actions = new[] { action }.ToBytecodes(),
        }.Sign(_signers[0]);
        var block3 = new BlockBuilder
        {
            Height = blockchain.Tip.Height + 1,
            PreviousBlockHash = blockchain.Tip.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
            Transactions = [tx],
        }.Create(_signers[1]);
        var blockCommit3 = TestUtils.CreateBlockCommit(block3);
        blockchain.Append(block3, blockCommit3);
        Assert.Equal(
            DumbAction.DumbCurrency * 5,
            blockchain.GetWorld().GetBalance(_addresses[0], DumbAction.DumbCurrency));
    }

    [Fact]
    public void MintAsset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _initWorld.MintAsset(_addresses[0], Value(0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _initWorld.MintAsset(_addresses[0], Value(0, -1)));

        World delta0 = _initWorld;
        // currencies[0] (AAA) allows everyone to mint
        delta0 = delta0.MintAsset(_addresses[2], Value(0, 10));
        Assert.Equal(Value(0, 10), delta0.GetBalance(_addresses[2], _currencies[0]));

        // currencies[2] (CCC) allows only _addr[0] to mint
        delta0 = delta0.MintAsset(_addresses[0], Value(2, 10));
        Assert.Equal(Value(2, 20), delta0.GetBalance(_addresses[0], _currencies[2]));

        // currencies[3] (DDD) allows _addr[0] & _addr[1] to mint
        delta0 = delta0.MintAsset(_addresses[1], Value(3, 10));
        Assert.Equal(Value(3, 30), delta0.GetBalance(_addresses[1], _currencies[3]));

        // currencies[5] (FFF) has a cap of 100
        Assert.Throws<SupplyOverflowException>(
            () => _initWorld.MintAsset(_addresses[0], Value(5, 200)));

        World delta1 = _initWorld with { Signer = _addresses[1] };
        // currencies[0] (DDD) allows everyone to mint
        delta1 = delta1.MintAsset(_addresses[2], Value(0, 10));
        Assert.Equal(Value(0, 10), delta1.GetBalance(_addresses[2], _currencies[0]));

        // currencies[2] (CCC) disallows _addr[1] to mint
        Assert.Throws<CurrencyPermissionException>(() =>
            delta1.MintAsset(_addresses[1], Value(2, 10)));

        // currencies[3] (DDD) allows _addr[0] & _addr[1] to mint
        delta1 = delta1.MintAsset(_addresses[0], Value(3, 20));
        Assert.Equal(Value(3, 20), delta1.GetBalance(_addresses[0], _currencies[3]));
    }

    [Fact]
    public void BurnAsset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _initWorld.BurnAsset(_addresses[0], Value(0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => _initWorld.BurnAsset(_addresses[0], Value(0, -1)));
        Assert.Throws<InsufficientBalanceException>(() => _initWorld.BurnAsset(_addresses[0], Value(0, 6)));

        World delta0 = _initWorld;
        // currencies[0] (AAA) allows everyone to burn
        delta0 = delta0.BurnAsset(_addresses[0], Value(0, 2));
        Assert.Equal(Value(0, 3), delta0.GetBalance(_addresses[0], _currencies[0]));

        // currencies[2] (CCC) allows only _addr[0] to burn
        delta0 = delta0.BurnAsset(_addresses[0], Value(2, 4));
        Assert.Equal(Value(2, 6), delta0.GetBalance(_addresses[0], _currencies[2]));

        // currencies[3] (DDD) allows _addr[0] & _addr[1] to burn
        delta0 = delta0.BurnAsset(_addresses[1], Value(3, 8));
        Assert.Equal(Value(3, 12), delta0.GetBalance(_addresses[1], _currencies[3]));

        World delta1 = _initWorld with { Signer = _addresses[1] };
        // currencies[0] (AAA) allows everyone to burn
        delta1 = delta1.BurnAsset(_addresses[0], Value(0, 2));
        Assert.Equal(Value(0, 3), delta1.GetBalance(_addresses[0], _currencies[0]));

        // currencies[2] (CCC) disallows _addr[1] to burn
        Assert.Throws<CurrencyPermissionException>(() =>
            delta1.BurnAsset(_addresses[0], Value(2, 4)));

        // currencies[3] (DDD) allows _addr[0] & _addr[1] to burn
        delta1 = delta1.BurnAsset(_addresses[1], Value(3, 8));
        Assert.Equal(Value(3, 12), delta1.GetBalance(_addresses[1], _currencies[3]));
    }

    [Fact]
    public void SetValidators()
    {
        const int newValidatorCount = 6;
        var world = _initWorld;
        var keys = Enumerable
            .Range(0, newValidatorCount)
            .Select(i => new PrivateKey())
            .ToList();

        var validatorSet = keys.Select(key => new Validator { Address = key.Address }).ToImmutableSortedSet();
        world = world.SetValidators(validatorSet);
        Assert.Equal(newValidatorCount, world.GetValidators().Count);
        Assert.NotEqual(_initWorld.GetValidators(), world.GetValidators());
        var expectedValue = (ImmutableSortedSet<Validator>)world.GetAccount(SystemAccount).Trie[$"{ValidatorsKey}"];
        Assert.Equal(world.GetValidators(), expectedValue);

        world = world.SetValidators([]);
        Assert.Empty(world.GetValidators());
        expectedValue = (ImmutableSortedSet<Validator>)world.GetAccount(SystemAccount).Trie[$"{ValidatorsKey}"];
        Assert.Equal(world.GetValidators(), expectedValue);
    }

    [Fact]
    public void TotalSupplyTracking()
    {
        World world = _initWorld;

        Assert.Equal(
            Value(0, 5),
            world.GetTotalSupply(_currencies[0]));

        Assert.Equal(
            Value(4, 5),
            _initWorld.GetTotalSupply(_currencies[4]));

        world = world.MintAsset(_addresses[0], Value(0, 10));
        Assert.Equal(
            Value(0, 15),
            world.GetTotalSupply(_currencies[0]));

        world = world.MintAsset(_addresses[0], Value(4, 10));
        Assert.Equal(
            Value(4, 15),
            world.GetTotalSupply(_currencies[4]));

        Assert.Throws<InsufficientBalanceException>(() =>
            world.BurnAsset(_addresses[0], Value(4, 100)));

        world = world.BurnAsset(_addresses[0], Value(4, 5));
        Assert.Equal(
            Value(4, 10),
            world.GetTotalSupply(_currencies[4]));
    }

    private FungibleAssetValue Value(int currencyIndex, BigInteger quantity)
        => FungibleAssetValue.Create(_currencies[currencyIndex], quantity, 0);
}
