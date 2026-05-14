using NAudio.Wave;

namespace ShadowPilot.Services;

// Captures system audio (loopback) — hears what the interviewer is saying
public sealed class SystemAudioCapture : IDisposable
{
    public Action<float[]>? OnBuffer;

    private WasapiLoopbackCapture? _capture;

    public void Start()
    {
        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += OnData;
        _capture.StartRecording();
    }

    public void Stop()
    {
        _capture?.StopRecording();
        _capture?.Dispose();
        _capture = null;
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

        var format = _capture!.WaveFormat;
        var samples = new float[e.BytesRecorded / (format.BitsPerSample / 8)];

        // Convert bytes to float samples (IEEE float or PCM 16-bit)
        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);
        }
        else
        {
            // 16-bit PCM fallback
            for (int i = 0; i < samples.Length; i++)
                samples[i] = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
        }

        // Downmix to mono if stereo
        if (format.Channels == 2)
        {
            var mono = new float[samples.Length / 2];
            for (int i = 0; i < mono.Length; i++)
                mono[i] = (samples[i * 2] + samples[i * 2 + 1]) * 0.5f;
            OnBuffer?.Invoke(mono);
        }
        else
        {
            OnBuffer?.Invoke(samples);
        }
    }

    public void Dispose() => Stop();
}
