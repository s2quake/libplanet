using Libplanet.State;
using Libplanet.State.Tests.Common;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using Libplanet.Types.Transactions;

namespace Libplanet.Tests.Action;

public sealed class WorldTest
{
    private readonly PrivateKey[] _keys;
    private readonly Address[] _addr;
    private readonly Currency[] _currencies;
    private readonly World _initWorld;
    private readonly IActionContext _initContext;

    public WorldTest()
    {
        _keys =
        [
            Currencies.MinterAKey,
            Currencies.MinterBKey,
            new PrivateKey(),
            new PrivateKey(),
        ];

        _addr = _keys.Select(key => key.Address).ToArray();

        _currencies =
        [
            Currencies.CurrencyA,
            Currencies.CurrencyB,
            Currencies.CurrencyC,
            Currencies.CurrencyD,
            Currencies.CurrencyE,
            Currencies.CurrencyF,
        ];

        World initMockWorldState = new World() with { Signer = _addr[0] };
        _initWorld = initMockWorldState
            .SetBalance(_addr[0], _currencies[0], 5)
            .SetBalance(_addr[0], _currencies[2], 10)
            .SetBalance(_addr[0], _currencies[4], 5)
            .SetBalance(_addr[1], _currencies[2], 15)
            .SetBalance(_addr[1], _currencies[3], 20)
            .SetValidators([.. _keys.Select(key => new Validator { Address = key.Address })]);

        _initContext = CreateContext(_addr[0]);
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
        Assert.Equal(Value(0, 5), _initWorld.GetBalance(_addr[0], _currencies[0]));
        Assert.Equal(Value(2, 10), _initWorld.GetBalance(_addr[0], _currencies[2]));
        Assert.Equal(Value(4, 5), _initWorld.GetBalance(_addr[0], _currencies[4]));
        Assert.Equal(Value(2, 15), _initWorld.GetBalance(_addr[1], _currencies[2]));
        Assert.Equal(Value(3, 20), _initWorld.GetBalance(_addr[1], _currencies[3]));

        // Exhaustive check for the rest.
        Assert.Equal(Value(1, 0), _initWorld.GetBalance(_addr[0], _currencies[1]));
        Assert.Equal(Value(3, 0), _initWorld.GetBalance(_addr[0], _currencies[3]));
        Assert.Equal(Value(5, 0), _initWorld.GetBalance(_addr[0], _currencies[5]));

        Assert.Equal(Value(0, 0), _initWorld.GetBalance(_addr[1], _currencies[0]));
        Assert.Equal(Value(1, 0), _initWorld.GetBalance(_addr[1], _currencies[1]));
        Assert.Equal(Value(4, 0), _initWorld.GetBalance(_addr[1], _currencies[4]));
        Assert.Equal(Value(5, 0), _initWorld.GetBalance(_addr[1], _currencies[5]));

        Assert.Equal(Value(0, 0), _initWorld.GetBalance(_addr[2], _currencies[0]));
        Assert.Equal(Value(1, 0), _initWorld.GetBalance(_addr[2], _currencies[1]));
        Assert.Equal(Value(2, 0), _initWorld.GetBalance(_addr[2], _currencies[2]));
        Assert.Equal(Value(3, 0), _initWorld.GetBalance(_addr[2], _currencies[3]));
        Assert.Equal(Value(4, 0), _initWorld.GetBalance(_addr[2], _currencies[4]));
        Assert.Equal(Value(5, 0), _initWorld.GetBalance(_addr[2], _currencies[5]));

        Assert.Equal(Value(0, 0), _initWorld.GetBalance(_addr[3], _currencies[0]));
        Assert.Equal(Value(1, 0), _initWorld.GetBalance(_addr[3], _currencies[1]));
        Assert.Equal(Value(2, 0), _initWorld.GetBalance(_addr[3], _currencies[2]));
        Assert.Equal(Value(3, 0), _initWorld.GetBalance(_addr[3], _currencies[3]));
        Assert.Equal(Value(4, 0), _initWorld.GetBalance(_addr[3], _currencies[4]));
        Assert.Equal(Value(5, 0), _initWorld.GetBalance(_addr[3], _currencies[5]));
    }

    [Fact]
    public void TransferAsset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _initWorld.TransferAsset(_addr[0], _addr[1], Value(0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _initWorld.TransferAsset(_addr[0], _addr[1], Value(0, -1)));
        Assert.Throws<InsufficientBalanceException>(() =>
            _initWorld.TransferAsset(_addr[0], _addr[1], Value(0, 6)));

        World world = _initWorld.TransferAsset(_addr[0], _addr[1], Value(0, 4));
        Assert.Equal(Value(0, 1), world.GetBalance(_addr[0], _currencies[0]));
        Assert.Equal(Value(0, 4), world.GetBalance(_addr[1], _currencies[0]));

        world = _initWorld.TransferAsset(_addr[0], _addr[0], Value(0, 2));
        Assert.Equal(Value(0, 5), world.GetBalance(_addr[0], _currencies[0]));
    }

    [Fact]
    public void TransferAssetInBlock()
    {
        var proposer = new PrivateKey();
        var blockChain = TestUtils.MakeBlockChain(
            protocolVersion: ProtocolVersion,
            privateKey: proposer);

        // Mint
        var action = DumbAction.Create(null, (null, _addr[1], 20));
        var tx = new TransactionMetadata
        {
            Signer = _keys[0].Address,
            GenesisHash = blockChain.Genesis.BlockHash,
            Actions = new[] { action }.ToBytecodes(),
        }.Sign(_keys[0]);
        var rawBlock1 = TestUtils.ProposeNext(
            previousBlock: blockChain.Tip,
            previousStateRootHash: blockChain.StateRootHash,
            transactions: [tx],
            proposer: proposer,
            protocolVersion: ProtocolVersion);
        var block1 = rawBlock1.Sign(proposer);
        var blockCommit1 = TestUtils.CreateBlockCommit(block1);
        blockChain.Append(block1, blockCommit1);
        Assert.Equal(
            DumbAction.DumbCurrency * 0,
            blockChain
                .GetWorld()
                .GetBalance(_addr[0], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 20,
            blockChain
                .GetWorld()
                .GetBalance(_addr[1], DumbAction.DumbCurrency));

        // Transfer
        action = DumbAction.Create(null, (_addr[1], _addr[0], 5));
        tx = new TransactionMetadata
        {
            Nonce = 1,
            Signer = _keys[0].Address,
            GenesisHash = blockChain.Genesis.BlockHash,
            Actions = new[] { action }.ToBytecodes(),
        }.Sign(_keys[0]);
        var rawBlock2 = TestUtils.ProposeNext(
            previousBlock: blockChain.Tip,
            previousStateRootHash: blockChain.StateRootHash,
            transactions: [tx],
            proposer: proposer,
            protocolVersion: ProtocolVersion,
            previousCommit: blockChain.BlockCommits[blockChain.Tip.Height]);
        var block2 = rawBlock2.Sign(proposer);
        var blockCommit2 = TestUtils.CreateBlockCommit(block2);
        blockChain.Append(block2, blockCommit2);
        Assert.Equal(
            DumbAction.DumbCurrency * 5,
            blockChain
                .GetWorld()
                .GetBalance(_addr[0], DumbAction.DumbCurrency));
        Assert.Equal(
            DumbAction.DumbCurrency * 15,
            blockChain
                .GetWorld()
                .GetBalance(_addr[1], DumbAction.DumbCurrency));

        // Transfer bugged
        action = DumbAction.Create((_addr[0], "a"), (_addr[0], _addr[0], 1));
        tx = new TransactionMetadata
        {
            Nonce = blockChain.GetNextTxNonce(_addr[0]),
            Signer = _keys[0].Address,
            GenesisHash = blockChain.Genesis.BlockHash,
            Actions = new[] { action }.ToBytecodes(),
        }.Sign(_keys[0]);
        var rawBlock3 = TestUtils.ProposeNext(
            previousBlock: blockChain.Tip,
            previousStateRootHash: blockChain.StateRootHash,
            transactions: [tx],
            proposer: _keys[1],
            protocolVersion: ProtocolVersion,
            previousCommit: blockChain.BlockCommits[blockChain.Tip.Height]);
        var block3 = rawBlock3.Sign(_keys[1]);
        var blockCommit3 = TestUtils.CreateBlockCommit(block3);
        blockChain.Append(block3, blockCommit3);
        Assert.Equal(
            DumbAction.DumbCurrency * 5,
            blockChain.GetWorld().GetBalance(_addr[0], DumbAction.DumbCurrency));
    }

    [Fact]
    public void MintAsset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _initWorld.MintAsset(_addr[0], Value(0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _initWorld.MintAsset(_addr[0], Value(0, -1)));

        World delta0 = _initWorld;
        IActionContext context0 = _initContext;
        // currencies[0] (AAA) allows everyone to mint
        delta0 = delta0.MintAsset(_addr[2], Value(0, 10));
        Assert.Equal(Value(0, 10), delta0.GetBalance(_addr[2], _currencies[0]));

        // currencies[2] (CCC) allows only _addr[0] to mint
        delta0 = delta0.MintAsset(_addr[0], Value(2, 10));
        Assert.Equal(Value(2, 20), delta0.GetBalance(_addr[0], _currencies[2]));

        // currencies[3] (DDD) allows _addr[0] & _addr[1] to mint
        delta0 = delta0.MintAsset(_addr[1], Value(3, 10));
        Assert.Equal(Value(3, 30), delta0.GetBalance(_addr[1], _currencies[3]));

        // currencies[5] (FFF) has a cap of 100
        Assert.Throws<SupplyOverflowException>(
            () => _initWorld.MintAsset(_addr[0], Value(5, 200)));

        World delta1 = _initWorld with { Signer = _addr[1] };
        IActionContext context1 = CreateContext(_addr[1]);
        // currencies[0] (DDD) allows everyone to mint
        delta1 = delta1.MintAsset(_addr[2], Value(0, 10));
        Assert.Equal(Value(0, 10), delta1.GetBalance(_addr[2], _currencies[0]));

        // currencies[2] (CCC) disallows _addr[1] to mint
        Assert.Throws<CurrencyPermissionException>(() =>
            delta1.MintAsset(_addr[1], Value(2, 10)));

        // currencies[3] (DDD) allows _addr[0] & _addr[1] to mint
        delta1 = delta1.MintAsset(_addr[0], Value(3, 20));
        Assert.Equal(Value(3, 20), delta1.GetBalance(_addr[0], _currencies[3]));
    }

    [Fact]
    public void BurnAsset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _initWorld.BurnAsset(_addr[0], Value(0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => _initWorld.BurnAsset(_addr[0], Value(0, -1)));
        Assert.Throws<InsufficientBalanceException>(() => _initWorld.BurnAsset(_addr[0], Value(0, 6)));

        World delta0 = _initWorld;
        IActionContext context0 = _initContext;
        // currencies[0] (AAA) allows everyone to burn
        delta0 = delta0.BurnAsset(_addr[0], Value(0, 2));
        Assert.Equal(Value(0, 3), delta0.GetBalance(_addr[0], _currencies[0]));

        // currencies[2] (CCC) allows only _addr[0] to burn
        delta0 = delta0.BurnAsset(_addr[0], Value(2, 4));
        Assert.Equal(Value(2, 6), delta0.GetBalance(_addr[0], _currencies[2]));

        // currencies[3] (DDD) allows _addr[0] & _addr[1] to burn
        delta0 = delta0.BurnAsset(_addr[1], Value(3, 8));
        Assert.Equal(Value(3, 12), delta0.GetBalance(_addr[1], _currencies[3]));

        World delta1 = _initWorld with { Signer = _addr[1] };
        IActionContext context1 = CreateContext(_addr[1]);
        // currencies[0] (AAA) allows everyone to burn
        delta1 = delta1.BurnAsset(_addr[0], Value(0, 2));
        Assert.Equal(Value(0, 3), delta1.GetBalance(_addr[0], _currencies[0]));

        // currencies[2] (CCC) disallows _addr[1] to burn
        Assert.Throws<CurrencyPermissionException>(() =>
            delta1.BurnAsset(_addr[0], Value(2, 4)));

        // currencies[3] (DDD) allows _addr[0] & _addr[1] to burn
        delta1 = delta1.BurnAsset(_addr[1], Value(3, 8));
        Assert.Equal(Value(3, 12), delta1.GetBalance(_addr[1], _currencies[3]));
    }

    [Fact]
    public void SetValidatorSet()
    {
        // const int newValidatorCount = 6;
        // var world = _initWorld;
        // var keys = Enumerable
        //     .Range(0, newValidatorCount)
        //     .Select(i => new PrivateKey())
        //     .ToList();

        // var validatorSet =
        //     keys.Select(key => Validator.Create(key.PublicKey, 1)).ToImmutableSortedSet();
        // world = world.SetValidatorSet(validatorSet);
        // Assert.Equal(newValidatorCount, world.GetValidatorSet().Count);
        // Assert.NotEqual(_initWorld.GetValidatorSet(), world.GetValidatorSet());
        // var oldValidatorSetRawValue = world
        //     .GetAccount(ReservedAddresses.LegacyAccount)
        //     .Trie[KeyConverters.ValidatorSetKey];
        // var newValidatorSetRawValue = world
        //     .GetAccount(ReservedAddresses.ValidatorSetAccount)
        //     .Trie[KeyConverters.ToStateKey(ValidatorSetAccount.ValidatorSetAddress)];
        // Assert.Null(oldValidatorSetRawValue);
        // Assert.NotNull(newValidatorSetRawValue);

        // world = world.SetValidatorSet([]);
        // Assert.Empty(world.GetValidatorSet());
        // oldValidatorSetRawValue =
        //     world.GetAccount(ReservedAddresses.LegacyAccount).Trie[
        //         KeyConverters.ValidatorSetKey];
        // newValidatorSetRawValue =
        //     world.GetAccount(ReservedAddresses.ValidatorSetAccount).Trie[
        //         KeyConverters.ToStateKey(ValidatorSetAccount.ValidatorSetAddress)];
        // Assert.Null(oldValidatorSetRawValue);
        // Assert.NotNull(newValidatorSetRawValue);
    }

    [Fact]
    public void TotalSupplyTracking()
    {
        World world = _initWorld;
        IActionContext context = _initContext;

        Assert.Equal(
            Value(0, 5),
            world.GetTotalSupply(_currencies[0]));

        Assert.Equal(
            Value(4, 5),
            _initWorld.GetTotalSupply(_currencies[4]));

        world = world.MintAsset(_addr[0], Value(0, 10));
        Assert.Equal(
            Value(0, 15),
            world.GetTotalSupply(_currencies[0]));

        world = world.MintAsset(_addr[0], Value(4, 10));
        Assert.Equal(
            Value(4, 15),
            world.GetTotalSupply(_currencies[4]));

        Assert.Throws<InsufficientBalanceException>(() =>
            world.BurnAsset(_addr[0], Value(4, 100)));

        world = world.BurnAsset(_addr[0], Value(4, 5));
        Assert.Equal(
            Value(4, 10),
            world.GetTotalSupply(_currencies[4]));
    }

    private FungibleAssetValue Value(int currencyIndex, BigInteger quantity)
        => FungibleAssetValue.Create(_currencies[currencyIndex], quantity, 0);
}
