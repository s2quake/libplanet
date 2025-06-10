using Xunit.Abstractions;

namespace Libplanet.Types.Tests;

public static partial class RandomUtility
{
    public static Random GetRandom(ITestOutputHelper output)
    {
        var seed = Int32();
        output.WriteLine($"Random seed: {seed}");
        return new Random(seed);
    }
}
