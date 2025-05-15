namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    public enum TestEnum
    {
        A,
        B,
        C,
    }

    public static IEnumerable<object[]> RandomSeeds =>
    [
        [Random.Shared.Next()],
        [Random.Shared.Next()],
        [Random.Shared.Next()],
        [Random.Shared.Next()],
    ];
}
