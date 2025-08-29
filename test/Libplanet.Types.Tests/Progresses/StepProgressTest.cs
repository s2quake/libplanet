using Libplanet.Types.Progresses;

namespace Libplanet.Types.Tests.Progresses;

public sealed class StepProgressTest(ITestOutputHelper output)
{
    [Fact]
    public void Ctor()
    {
        var progress = new StepProgress(10);

        for (var i = 0; i < 10; i++)
        {
            progress.Next($"Step {i + 1}");
            Assert.Equal(i, progress.Step);
        }

        progress.Complete("Completed");
        Assert.Equal(10, progress.Step);
    }

    [Fact]
    public void SubProgress()
    {
        const int count = 2;
        var itemList = new List<ProgressInfo>(100);
        var progress = new StepProgress(count);
        progress.ProgressChanged += (s, e) =>
        {
            itemList.Add(e);
            output.WriteLine($"Progress: {e.Value:P2} - {e.Text}");
        };

        for (var i = 0; i < count; i++)
        {
            var subProgress = progress.BeginSubProgress(count);
            for (var j = 0; j < count; j++)
            {
                var subsubProgress = subProgress.BeginSubProgress(count);
                for (var k = 0; k < count; k++)
                {
                    subsubProgress.Next($"SubSubStep {i}, {j}, {k + 1}, {i * 4 + j * 2 + k}");
                    Assert.Equal(k, subsubProgress.Step);
                }

                subsubProgress.Complete($"SubSubCompleted {i}, {j}");
                Assert.Equal(count, subsubProgress.Step);
            }

            subProgress.Complete($"SubCompleted {i}");
            Assert.Equal(count, subProgress.Step);
        }

        progress.Complete("Completed");
        Assert.Equal(count, progress.Step);
    }
}
