namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest(ITestOutputHelper output)
{
    public enum TestEnum
    {
        A,
        B,
        C,
    }

    public static TheoryData<int> RandomSeeds =>
    [
        Random.Shared.Next(),
        Random.Shared.Next(),
        Random.Shared.Next(),
        Random.Shared.Next(),
    ];
}
