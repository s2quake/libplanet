using Libplanet.KeyStore;
using Libplanet.Types.Crypto;

namespace Libplanet.Tests.KeyStore
{
    public class MismatchedAddressExceptionTest
    {
        [Fact]
        public void Constructor()
        {
            Address
                expectedAddress = default,
                actualAddress = Address.Parse("01234567789aBcdEF01234567789ABCdeF012345");
            var e = new MismatchedAddressException(
                "Some message.",
                expectedAddress,
                actualAddress
            );
            Assert.Equal(
                "Some message.\nExpected address: 0x0000000000000000000000000000000000000000\n" +
                "Actual address: 0x01234567789aBcdEF01234567789ABCdeF012345",
                e.Message
            );
            Assert.Equal(expectedAddress, e.ExpectedAddress);
            Assert.Equal(actualAddress, e.ActualAddress);
        }
    }
}
