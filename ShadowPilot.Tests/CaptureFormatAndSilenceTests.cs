using NAudio.Wave;
using ShadowPilot.Services;
using Xunit;

namespace ShadowPilot.Tests;

public sealed class CaptureFormatAndSilenceTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void PcmStereoDownmixUsesWholeFramesAndOnlyRecordedBytes(int bits)
    {
        var converter = new LoopbackSampleConverter(new WaveFormat(48000, bits, 2));
        var bytes = new byte[bits / 8 * 4];
        // First frame = (-1, 0), second frame is unused capture buffer capacity.
        bytes[bits / 8 - 1] = 0x80;
        Assert.Equal(new[] { -0.5f }, converter.Convert(bytes, bits / 8 * 2));
    }

    [Fact]
    public void ExtensibleFloatSurroundIsDownmixedToOneSamplePerFrame()
    {
        var converter = new LoopbackSampleConverter(new WaveFormatExtensible(48000, 32, 6));
        float[] values = [1, 0, 0, 0, 0, -1, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f];
        var bytes = new byte[values.Length * 4];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        Assert.Equal(new[] { 0f, 0.5f }, converter.Convert(bytes, bytes.Length));
    }

    [Fact]
    public void UnsupportedFormatAndIncompleteFramesAreRejected()
    {
        Assert.Throws<NotSupportedException>(() => new LoopbackSampleConverter(new WaveFormat(48000, 8, 1)));
        var converter = new LoopbackSampleConverter(new WaveFormat(48000, 16, 2));
        Assert.Throws<ArgumentException>(() => converter.Convert(new byte[3], 3));
    }

    [Fact]
    public void AutoListenWaitsForSpeechThenFiresOnceEvenWithoutMoreCapturePackets()
    {
        long now = 0;
        var detector = new SilenceDetector(1.8, () => now);
        var fired = 0;
        detector.OnSilence = () => fired++;
        detector.Process([0, 0]);
        now = 5000;
        detector.CheckSilence();
        Assert.Equal(0, fired);
        detector.Process([0.2f]);
        now += 1799;
        detector.CheckSilence();
        Assert.Equal(0, fired);
        now++;
        detector.CheckSilence();
        detector.CheckSilence();
        Assert.Equal(1, fired);
        detector.Reset();
        now += 2000;
        detector.CheckSilence();
        Assert.Equal(1, fired);
        detector.Process([0.2f]);
        now += 1800;
        detector.CheckSilence();
        Assert.Equal(2, fired);
    }

    [Fact]
    public void ContinuingSpeechResetsSilenceDeadline()
    {
        long now = 0;
        var detector = new SilenceDetector(1.8, () => now);
        var fired = false;
        detector.OnSilence = () => fired = true;
        detector.Process([0.5f]);
        now = 1700;
        detector.Process([0.5f]);
        now = 1800;
        detector.Process([0]);
        detector.CheckSilence();
        Assert.False(fired);
        now = 3500;
        detector.CheckSilence();
        Assert.True(fired);
    }
}
