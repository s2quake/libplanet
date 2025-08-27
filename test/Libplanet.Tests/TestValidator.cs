using Libplanet.Types;

namespace Libplanet.Tests;

public sealed class TestValidator(ISigner signer, BigInteger power) : ISigner, IComparable<TestValidator>, IComparable
{
    private readonly Validator _validator = new()
    {
        Address = signer.Address,
        Power = power,
    };

    public TestValidator(ISigner signer)
        : this(signer, BigInteger.One)
    {
    }

    public BigInteger Power => _validator.Power;

    public Address Address => _validator.Address;

    public static implicit operator Validator(TestValidator v) => v._validator;

    public override string ToString() => $"{Address}:{Power}";

    byte[] ISigner.Sign(Span<byte> message) => signer.Sign(message);

    int IComparable<TestValidator>.CompareTo(TestValidator? other) => _validator.CompareTo(other?._validator);

    public int CompareTo(object? obj) => _validator.CompareTo(obj);
}
