using Libplanet.Crypto;

namespace Libplanet.Action.State;

public static class ReservedAddresses
{
    public static readonly Address LegacyAccount =
        Address.Parse("1000000000000000000000000000000000000000");

    public static readonly Address ValidatorSetAccount =
        Address.Parse("1000000000000000000000000000000000000001");
}
