using Libplanet.State;
using Libplanet.State.Tests.Common;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Transactions;

namespace Libplanet.Tests.Tx;

public class TransactionExtensionsTest
{
    private static readonly Address AddressA =
        Address.Parse("D6D639DA5a58A78A564C2cD3DB55FA7CeBE244A9");

    private static readonly Address AddressB =
        Address.Parse("B61CE2Ce6d28237C1BC6E114616616762f1a12Ab");

    [Fact]
    public void Sign()
    {
        var genesisHash = BlockHash.Parse(
            "92854cf0a62a7103b9c610fd588ad45254e64b74ceeeb209090ba572a41bf265");
        var updatedAddresses = ImmutableSortedSet.Create(AddressA, AddressB);
        var timestamp = new DateTimeOffset(2023, 3, 29, 1, 2, 3, 456, TimeSpan.Zero);
        var actions = ImmutableArray.Create<IAction>([
            DumbAction.Create((AddressA, "foo")),
            DumbAction.Create((AddressB, "bar")),
        ]).ToImmutableArray();
        var privateKey =
            PrivateKey.Parse("51fb8c2eb261ed761429c297dd1f8952c8ce327d2ec2ec5bcc7728e3362627c2");
        Transaction tx = new TransactionMetadata
        {
            GenesisHash = genesisHash,
            Signer = privateKey.Address,
            Timestamp = timestamp,
            Actions = actions.ToBytecodes(),
            Nonce = 123L,
        }.Sign(privateKey);
        // Assert.Equal(invoice, tx.UnsignedTx.Invoice);
        // Assert.Equal(
        //     TxSigningMetadata.Create(privateKey.PublicKey, 123L), tx.UnsignedTx.SigningMetadata);
        // Assert.True(tx.Verify(privateKey.Address));
    }
}
