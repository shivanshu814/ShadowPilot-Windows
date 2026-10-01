using NAudio.Wave;
using ShadowPilot.Services;
using Xunit;

namespace ShadowPilot.Tests;

public sealed class AudioRoutingTests
{
    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public async Task CapturedStereoSamplesReachRecognitionAsMonoPcmAtTheirOriginalRate(int rate)
    {
        var engine = new FakeEngine();
        using var recognizer = new SpeechRecognizerService(() => engine);
        var append = recognizer.Start(rate, _ => { }, _ => { });
        var converter = new LoopbackSampleConverter(WaveFormat.CreateIeeeFloatWaveFormat(rate, 2));
        float[] interleaved = [1, 0, -1, 0, 0.25f, -0.25f];
        var captured = new byte[interleaved.Length * 4];
        Buffer.BlockCopy(interleaved, 0, captured, 0, captured.Length);
        append(converter.Convert(captured, captured.Length));
        Assert.Equal(rate, engine.SampleRate);
        var bytes = new byte[6];
        Assert.Equal(6, engine.Audio!.Read(bytes, 0, bytes.Length));
        Assert.Equal(new byte[] { 0, 64, 0, 192, 0, 0 }, bytes);
        await Finish(recognizer, engine);
    }

    [Fact]
    public async Task AppendSamplesFeedsTheSameStreamAndClipsToLittleEndianPcm16()
    {
        var engine = new FakeEngine();
        using var recognizer = new SpeechRecognizerService(() => engine);
        recognizer.Start(16000, _ => { }, _ => { });
        recognizer.AppendSamples([-2, -1, 0, 1, 2, float.NaN]);
        var bytes = new byte[12];
        Assert.Equal(bytes.Length, engine.Audio!.Read(bytes, 0, bytes.Length));
        Assert.Equal(new byte[] { 0, 128, 0, 128, 0, 0, 255, 127, 255, 127, 0, 0 }, bytes);
        await Finish(recognizer, engine);
    }

    [Fact]
    public async Task EmptyLiveStreamWaitsForDataAndCompletionDrainsBeforeEof()
    {
        using var stream = new PcmAudioStream(16);
        var bytes = new byte[4];
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = Task.Run(() => { entered.SetResult(); return stream.Read(bytes, 0, 4); });
        await entered.Task;
        Assert.NotSame(read, await Task.WhenAny(read, Task.Delay(50)));
        stream.AppendSamples([0.5f, -0.5f]);
        stream.Complete();
        Assert.Equal(4, await read.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(new byte[] { 0, 64, 0, 192 }, bytes);
        Assert.Equal(0, stream.Read(bytes, 0, 4));
        stream.AppendSamples([1]);
        Assert.Equal(0, stream.Read(bytes, 0, 4));
    }

    [Fact]
    public async Task DisposeUnblocksPendingRead()
    {
        var stream = new PcmAudioStream(16);
        var read = Task.Run(() => stream.Read(new byte[2], 0, 2));
        stream.Dispose();
        Assert.Equal(0, await read.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void OverflowFailsExplicitlyWithoutDroppingOrGrowingAudio()
    {
        using var stream = new PcmAudioStream(2);
        stream.AppendSamples([0.5f]);
        Assert.Throws<IOException>(() => stream.AppendSamples([1]));
        var bytes = new byte[2];
        Assert.Equal(2, stream.Read(bytes, 0, 2));
        Assert.Equal(new byte[] { 0, 64 }, bytes);
    }

    [Fact]
    public async Task StopWaitsForFinalWordsAndHypothesesDoNotAccumulate()
    {
        var engine = new FakeEngine();
        var updates = new List<string>();
        using var recognizer = new SpeechRecognizerService(() => engine);
        recognizer.Start(48000, updates.Add, _ => { });
        engine.Hypothesis("what");
        engine.Hypothesis("what is");
        engine.Final("what is");
        engine.Hypothesis("your name");
        var stop = recognizer.StopAsync();
        Assert.Same(stop, recognizer.StopAsync());
        Assert.False(stop.IsCompleted);
        Assert.False(engine.Disposed);
        engine.Final("your name");
        engine.Complete();
        Assert.Equal("what is your name", await stop);
        Assert.Equal(new[] { "what", "what is", "what is", "what is your name", "what is your name" }, updates);
        Assert.True(engine.Disposed);
    }

    [Fact]
    public async Task RestartHasFreshAudioAndTextAndIgnoresOldCallbacks()
    {
        var old = new FakeEngine();
        var next = new FakeEngine();
        var engines = new Queue<FakeEngine>([old, next]);
        using var recognizer = new SpeechRecognizerService(() => engines.Dequeue());
        var updates = new List<string>();
        var staleAppend = recognizer.Start(48000, updates.Add, _ => { });
        old.Final("old");
        await Finish(recognizer, old);
        var append = recognizer.Start(44100, updates.Add, _ => { });
        staleAppend([1]);
        old.Final("late");
        append([0]);
        var bytes = new byte[2];
        Assert.Equal(2, next.Audio!.Read(bytes, 0, 2));
        Assert.Equal(new byte[] { 0, 0 }, bytes);
        next.Final("new");
        Assert.Equal(new[] { "old", "new" }, updates);
        Assert.Equal("new", await Finish(recognizer, next));
    }

    [Fact]
    public async Task FailedStartDisposesAndAllowsRetry()
    {
        var failed = new FakeEngine { FailStart = true };
        var next = new FakeEngine();
        var engines = new Queue<FakeEngine>([failed, next]);
        using var recognizer = new SpeechRecognizerService(() => engines.Dequeue());
        Assert.Throws<IOException>(() => recognizer.Start(16000, _ => { }, _ => { }));
        Assert.True(failed.Disposed);
        recognizer.Start(16000, _ => { }, _ => { });
        Assert.Throws<InvalidOperationException>(() => recognizer.Start(16000, _ => { }, _ => { }));
        await Finish(recognizer, next);
    }

    [Fact]
    public async Task StopTimeoutCancelsAndDisposesEngine()
    {
        var engine = new FakeEngine();
        using var recognizer = new SpeechRecognizerService(() => engine, TimeSpan.FromMilliseconds(50));
        recognizer.Start(16000, _ => { }, _ => { });
        await Assert.ThrowsAsync<TimeoutException>(() => recognizer.StopAsync());
        Assert.True(engine.Cancelled);
        Assert.True(engine.Disposed);
        Assert.Equal("", await recognizer.StopAsync());
    }

    [Fact]
    public async Task BackendFailureIsReportedAndCleanedUp()
    {
        var engine = new FakeEngine();
        Exception? reported = null;
        using var recognizer = new SpeechRecognizerService(() => engine);
        recognizer.Start(16000, _ => { }, error => reported = error);
        var error = new IOException("device lost");
        engine.Complete(error);
        Assert.Same(error, reported);
        await Assert.ThrowsAsync<IOException>(() => recognizer.StopAsync());
        Assert.True(engine.Disposed);
    }

    [Fact]
    public void DisposeIsIdempotentAndLateSamplesAreHarmless()
    {
        var engine = new FakeEngine();
        var recognizer = new SpeechRecognizerService(() => engine);
        var append = recognizer.Start(16000, _ => { }, _ => { });
        recognizer.Dispose();
        recognizer.Dispose();
        append([1]);
        recognizer.AppendSamples([1]);
        Assert.True(engine.Disposed);
        Assert.Equal(0, engine.Audio!.Read(new byte[2], 0, 2));
        Assert.Throws<ObjectDisposedException>(() => recognizer.Start(16000, _ => { }, _ => { }));
    }

    private static async Task<string> Finish(SpeechRecognizerService recognizer, FakeEngine engine)
    {
        var stop = recognizer.StopAsync();
        engine.Complete();
        return await stop.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private sealed class FakeEngine : IRecognitionEngine
    {
        public event Action<string>? Recognized;
        public event Action<string>? Hypothesized;
        public event Action<Exception?>? Completed;
        public Stream? Audio;
        public int SampleRate;
        public bool Disposed, Cancelled, FailStart;
        public void Start(Stream audio, int sampleRate)
        {
            Assert.Equal(long.MaxValue, audio.Length); // Required by System.Speech COM wrapper.
            Assert.Equal(0, audio.Seek(0, SeekOrigin.Begin));
            Audio = audio;
            SampleRate = sampleRate;
            if (FailStart) throw new IOException("start failed");
        }
        public void Final(string text) => Recognized?.Invoke(text);
        public void Hypothesis(string text) => Hypothesized?.Invoke(text);
        public void Complete(Exception? error = null) => Completed?.Invoke(error);
        public void Cancel() => Cancelled = true;
        public void Dispose() => Disposed = true;
    }
}
