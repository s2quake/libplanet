namespace Libplanet.Serialization.Json.Tests;

public sealed partial class ModelJsonSerializerTest(ITestOutputHelper output)
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
