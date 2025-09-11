using Libplanet.Serialization.Json.Schema;

namespace Libplanet.Serialization.Json.Tests.Schema;

public sealed class ModelSchemaBuilderTest
{
    [Fact]
    public async Task GetSchemaAsync()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var json = await ModelSchemaBuilder.GetSchemaAsync(cancellation);

        Assert.NotNull(json);
    }
}

[Model(Version = 1, TypeName = "ModelSchemaBuilderTest_TestModel")]
public sealed record class TestModel
{
    [Property(0)]
    public int Value { get; init; }
}

