namespace Libplanet.Types.Progresses;

public sealed class StepProgress : Progress<ProgressInfo>
{
    private const int DefaultMaxUpdateRate = 120;
    private readonly double _delta;
    private readonly StepProgress? _parent;
    private readonly int _parentStep;
    private DateTimeOffset _lastUpdate = DateTimeOffset.MinValue;
    private int _maxUpdateRate = DefaultMaxUpdateRate;
    private TimeSpan _minUpdateInterval = TimeSpan.FromSeconds(1.0d / DefaultMaxUpdateRate);

    public StepProgress(int length)
        : this(length, null)
    {
    }

    public StepProgress(int length, IProgress<ProgressInfo> progress)
        : this(length, null)
    {
        ProgressChanged += (s, e) =>
        {
            var step = Step;
            var delta = 1.0d / length;
            var value = step == length ? 1.0d : (step * delta);
            progress.Report(new ProgressInfo(value, e.Text));
        };
    }

    private StepProgress(int length, StepProgress? parent)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "The length must be greater than 0.");
        }

        _delta = 1.0d / length;
        Length = length;
        _parent = parent;
        _parentStep = parent is null ? -1 : parent.Step + 1;
    }

    public int Length { get; }

    public int Step { get; private set; } = -1;

    public int MaxUpdateRate
    {
        get => _maxUpdateRate;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The value must be greater than 0.");
            }

            _maxUpdateRate = value;
            _minUpdateInterval = TimeSpan.FromSeconds(1.0d / value);
        }
    }

    public StepProgress BeginSubProgress(int length)
    {
        if (Step == Length)
        {
            throw new InvalidOperationException("The progress has already been completed.");
        }

        return new StepProgress(length, this);
    }

    public void Next(string text)
    {
        if (Step >= Length - 1)
        {
            throw new InvalidOperationException("The progress has already been completed.");
        }

        var now = DateTimeOffset.UtcNow;
        var step = Step + 1;
        var item = new ProgressInfo((double)step / Length, text);
        Step = step;

        if (now - _lastUpdate >= _minUpdateInterval)
        {
            _lastUpdate = now;
            OnReport(item);
        }

        _parent?.BubbleNext(_parentStep, item);
    }

    public void Complete(string text)
    {
        if (Step == Length)
        {
            throw new InvalidOperationException("The progress has already been completed.");
        }

        var item = new ProgressInfo(1.0d, text);
        Step = Length;
        OnReport(item);
        _parent?.BubbleComplete(_parentStep, item);
    }

    private void BubbleNext(int step, ProgressInfo e)
    {
        var progress = (step * _delta) + (_delta * e.Value);
        var item = new ProgressInfo(progress, e.Text);
        OnReport(item);
        _parent?.BubbleNext(_parentStep, item);
    }

    private void BubbleComplete(int step, ProgressInfo e)
    {
        var progress = (step + 1) * _delta;
        var item = new ProgressInfo(progress, e.Text);
        Step = step;
        OnReport(item);
        _parent?.BubbleNext(_parentStep, item);
    }
}
