namespace Libplanet.Types.Progresses;

public static class ProgressExtensions
{
    // public static ForEachProgress<string> ForEach(
    //     this IProgress<ProgressInfo> @this, double begin, double end, int length)
    // {
    //     if (begin > end)
    //     {
    //         throw new ArgumentException(
    //             $"{nameof(begin)} must be less than or equal to {nameof(end)}.", nameof(begin));
    //     }

    //     if (end > 1)
    //     {
    //         throw new ArgumentException(
    //             $"{nameof(end)} must be less than or equal to 1.", nameof(end));
    //     }

    //     var lengthProgress = new ForEachProgress<string>(length);
    //     lengthProgress.ProgressChanged += (s, e) =>
    //     {
    //         var completion = lengthProgress.Completion;
    //         var gap = (end - begin) / length;
    //         var progress = completion == length ? end : (completion * gap) + begin;
    //         @this.Report(new ProgressInfo(progress, e));
    //     };

    //     return lengthProgress;
    // }

    public static StepProgress Step(this IProgress<ProgressInfo> @this, int length)
    {
        // if (begin > end)
        // {
        //     throw new ArgumentException(
        //         $"{nameof(begin)} must be less than or equal to {nameof(end)}.", nameof(begin));
        // }

        // if (end > 1)
        // {
        //     throw new ArgumentException($"{nameof(end)} must be less than or equal to 1.", nameof(end));
        // }

        var stepProgress = new StepProgress(length);
        stepProgress.ProgressChanged += (s, e) =>
        {
            var step = stepProgress.Step;
            var delta = 1.0d / length;
            var progress = step == length ? 1.0d : (step * delta);
            @this.Report(new ProgressInfo(progress, e.Text));
        };

        return stepProgress;
    }
}
