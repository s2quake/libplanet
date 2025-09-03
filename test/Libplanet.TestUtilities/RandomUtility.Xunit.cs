namespace Libplanet.TestUtilities;

public static partial class RandomUtility
{
    public static Random GetRandom(ITestOutputHelper output)
    {
        var seed = Int32();
        output.WriteLine($"Random seed: {seed}");
        return new Random(seed);
    }

    public static Random GetRandom(ITestOutputHelper output, int seed)
    {
        output.WriteLine($"Random seed: {seed}");
        return new Random(seed);
    }

    public static Random GetStaticRandom(ITestOutputHelper output)
    {
        var seed = 0;
        output.WriteLine($"Random seed: {seed}");
        return new Random(seed);
    }
}
