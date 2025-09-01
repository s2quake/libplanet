using Libplanet.Types.Progresses;

namespace Libplanet.Types.Tests.Progresses;

public sealed class StepProgressTest(ITestOutputHelper output)
{
    [Fact]
    public void BaseTest()
    {
        var progress = new StepProgress(10);

        Assert.Equal(-1, progress.Step);
        Assert.Equal(10, progress.Length);
        Assert.Equal(120, progress.MaxUpdateRate);

        for (var i = 0; i < 10; i++)
        {
            progress.Next($"Step {i + 1}");
            Assert.Equal(i, progress.Step);
        }

        progress.Complete("Completed");
        Assert.Equal(10, progress.Step);
    }

    [Fact]
    public async Task ProcessConstructor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var progress = new Progress<ProgressInfo>();
        var stepProgress = new StepProgress(2, progress);

        var e1 = await Assert.RaisesAsync<ProgressInfo>(
            handler => progress.ProgressChanged += handler,
            handler => progress.ProgressChanged -= handler,
            async () =>
            {
                stepProgress.Next("a");
                await Task.Delay(100, cancellationToken);
            });
        Assert.Equal("a", e1.Arguments.Text);
        Assert.Equal(0.0d, e1.Arguments.Value);

        var e2 = await Assert.RaisesAsync<ProgressInfo>(
            handler => progress.ProgressChanged += handler,
            handler => progress.ProgressChanged -= handler,
            async () =>
            {
                stepProgress.Next("b");
                await Task.Delay(100, cancellationToken);
            });
        Assert.Equal("b", e2.Arguments.Text);
        Assert.Equal(0.5d, e2.Arguments.Value);

        var e3 = await Assert.RaisesAsync<ProgressInfo>(
            handler => progress.ProgressChanged += handler,
            handler => progress.ProgressChanged -= handler,
            async () =>
            {
                stepProgress.Complete("Completed");
                await Task.Delay(100, cancellationToken);
            });
        Assert.Equal("Completed", e3.Arguments.Text);
        Assert.Equal(1.0d, e3.Arguments.Value);
    }

    [Fact]
    public void ZeroOrNegativeLength_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StepProgress(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StepProgress(-1));
    }

    [Fact]
    public async Task SubProgress()
    {
        int[] lengths = [2, 5, 10];
        var cancellationToken = TestContext.Current.CancellationToken;
        var itemList = new List<ProgressInfo>(1000);
        var progress = new StepProgress(lengths[0]) { MaxUpdateRate = int.MaxValue };
        var expected = 0;
        progress.ProgressChanged += (s, e) =>
        {
            itemList.Add(e);
        };

        void Invoke(int[] lengths, int depth, StepProgress progress, ref int expected)
        {
            var length = progress.Length;
            var indent = string.Empty.PadRight(depth * 2);
            for (var i = 0; i < length; i++)
            {
                if (depth + 1 >= lengths.Length)
                {
                    var text = $"{indent}Step {i}";
                    output.WriteLine(text);
                    progress.Next(text);
                    expected++;
                    Assert.Equal(i, progress.Step);
                }
                else
                {
                    var nextLength = lengths[depth + 1];
                    var subProgress = progress.BeginSubProgress(nextLength);
                    Invoke(lengths, depth + 1, subProgress, ref expected);
                    Assert.Equal(nextLength, subProgress.Step);
                }
            }

            var completedText = $"{indent}Completed";
            output.WriteLine(completedText);
            progress.Complete(completedText);
            expected++;
            Assert.Equal(length, progress.Step);
        }

        Invoke(lengths, 0, progress, ref expected);

        await Task.Delay(1000, cancellationToken);
        Assert.Equal(expected, itemList.Count);
    }

    [Theory]
    [InlineData(144)]
    [InlineData(120)]
    [InlineData(60)]
    public async Task MaxUpdateRate(int maxUpdateRate)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var stepProgress = new StepProgress(10000)
        {
            MaxUpdateRate = maxUpdateRate,
        };
        Assert.Equal(maxUpdateRate, stepProgress.MaxUpdateRate);

        var count = 0;
        var dateTime = DateTimeOffset.UtcNow;
        stepProgress.ProgressChanged += (s, e) =>
        {
            count++;
        };
        while (DateTimeOffset.UtcNow - dateTime < TimeSpan.FromSeconds(1))
        {
            stepProgress.Next("Step");
            await Task.Delay(1, cancellationToken);
        }

        await Task.Delay(1000, cancellationToken);

        output.WriteLine($"Count: {count}, MaxUpdateRate: {maxUpdateRate}");
        Assert.True(count <= maxUpdateRate);
    }

    [Fact]
    public void MaxUpdateRate_ZeroOrNegative_Throw()
    {
        var stepProgress = new StepProgress(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => stepProgress.MaxUpdateRate = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => stepProgress.MaxUpdateRate = -1);
    }

    [Fact]
    public void Next()
    {
        var stepProgress = new StepProgress(2);
        stepProgress.Next("Step 0");
        Assert.Equal(0, stepProgress.Step);
    }

    [Fact]
    public void Next_Throw()
    {
        var stepProgress = new StepProgress(1);
        stepProgress.Next("Step 0");
        Assert.Throws<InvalidOperationException>(() => stepProgress.Next("Step 1"));
    }

    [Fact]
    public void Next_AfterComplete_Throw()
    {
        var stepProgress = new StepProgress(1);
        stepProgress.Complete("Completed");
        Assert.Throws<InvalidOperationException>(() => stepProgress.Next("Step 0"));
    }

    [Fact]
    public void Complete()
    {
        var stepProgress = new StepProgress(1);
        stepProgress.Complete("Completed");
        Assert.Equal(1, stepProgress.Step);
    }

    [Fact]
    public void Complete_Twice_Throw()
    {
        var stepProgress = new StepProgress(1);
        stepProgress.Complete("Completed");
        Assert.Throws<InvalidOperationException>(() => stepProgress.Complete("Completed"));
    }

    [Fact]
    public void BeginSubProgress_After_Completed()
    {
        var stepProgress = new StepProgress(1);
        stepProgress.Complete("Completed");
        Assert.Throws<InvalidOperationException>(() => stepProgress.BeginSubProgress(1));
    }
}
