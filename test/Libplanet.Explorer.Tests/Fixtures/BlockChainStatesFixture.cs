using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet.Explorer.Tests.Fixtures;

public static class BlockChainStatesFixture
{
    public static readonly Address Address =
        Address.Parse("0x5003712B63baAB98094aD678EA2B24BcE445D076");

    public static readonly Currency Currency = Currency.Create("ABC", 2);

    public static readonly FungibleAssetValue Amount =
        FungibleAssetValue.Create(Currency, 123);

    public static readonly FungibleAssetValue AdditionalSupply =
        FungibleAssetValue.Create(Currency, 10000);

    public static readonly object Value = "Foo";

    public static readonly Validator Validator = new()
    {
        Address = Address.Parse("032038e153d344773986c039ba5dbff12ae70cfdf6ea8beb7c5ea9b361a72a9233"),
    };

    public static readonly ImmutableSortedSet<Validator> Validators =
        ImmutableSortedSet.Create([Validator]);

    public static (Blockchain, BlockHash, HashDigest<SHA256>)
        CreateMockBlockChainStates()
    {
        throw new NotImplementedException("Mocking BlockChainStates is not implemented yet.");
        // BlockChainStates mockBlockChainStates = new BlockChainStates();
        // World mock = World.Create(mockBlockChainStates.StateStore);
        // mock = mock
        //     .SetBalance(Address, Amount)
        //     .SetBalance(new PrivateKey().Address, AdditionalSupply)
        //     .SetValidatorSet(Validators);
        // Account account = mock.GetAccount(ReservedAddresses.LegacyAccount);
        // account = account.SetValue(Address, Value);
        // mock = mock.SetAccount(ReservedAddresses.LegacyAccount, account);

        // var blockHash = new BlockHash(Rand.Bytes(BlockHash.Size));
        // var stateRootHash = mock.Trie.Hash;
        // mockBlockChainStates.AttachBlockHashToStateRootHash(blockHash, stateRootHash);
        // return (mockBlockChainStates, blockHash, stateRootHash);
    }
}
