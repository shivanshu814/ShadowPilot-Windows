namespace ShadowPilot.Services;

public sealed class SilenceDetector
{
    public Action? OnSilence;

    private readonly double _threshold;
    private const float RmsFloor = 0.01f;
    private DateTime? _silenceStart;
    private bool _fired;

    public SilenceDetector(double thresholdSeconds = 1.8)
    {
        _threshold = thresholdSeconds;
    }

    public void Reset()
    {
        _silenceStart = null;
        _fired = false;
    }

    public void Process(float[] samples)
    {
        if (_fired || samples.Length == 0) return;

        float sum = 0;
        foreach (var s in samples) sum += s * s;
        var rms = MathF.Sqrt(sum / samples.Length);

        if (rms < RmsFloor)
        {
            _silenceStart ??= DateTime.UtcNow;
            if ((DateTime.UtcNow - _silenceStart.Value).TotalSeconds >= _threshold)
            {
                _fired = true;
                OnSilence?.Invoke();
            }
        }
        else
        {
            _silenceStart = null;
        }
    }
}
