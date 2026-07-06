using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Media;

namespace Broiler.Media.Image.Tests;

internal static class Program
{
    private static async Task<int> Main()
    {
        var tests = new List<(string Name, Func<ValueTask> Body)>
        {
            ("ImageCodec rejects non-image descriptors", CodecKindGuard),
            ("ImageBuffer validates RGBA stride and length", ImageBufferShape),
            ("ImageSequence wraps still frames", StaticSequence),
            ("ImageCodec default encode path is explicit unsupported", DefaultEncodeUnsupported),
        };

        int passed = 0;
        var failures = new List<string>();
        Console.WriteLine($"Running {tests.Count} image contract test(s)...\n");

        foreach ((string name, Func<ValueTask> body) in tests)
        {
            try
            {
                await body().ConfigureAwait(false);
                passed++;
                Console.WriteLine($"  [PASS] {name}");
            }
            catch (Exception ex)
            {
                failures.Add(name);
                Console.WriteLine($"  [FAIL] {name}");
                Console.WriteLine($"         {ex.GetType().Name}: {ex.Message}");
            }
        }

        Console.WriteLine($"\n{passed}/{tests.Count} passed, {failures.Count} failed.");
        return failures.Count;
    }

    private static ValueTask CodecKindGuard()
    {
        Assert.Throws<ArgumentException>(() => _ = new FakeImageCodec(MediaKind.Audio));
        _ = new FakeImageCodec(MediaKind.Image);
        return ValueTask.CompletedTask;
    }

    private static ValueTask ImageBufferShape()
    {
        var buffer = new ImageBuffer(
            2,
            2,
            ImagePixelFormat.Rgba8,
            ImageAlphaMode.Straight,
            stride: 8,
            pixels: new byte[16]);

        Assert.Equal(2, buffer.Width);
        Assert.Equal(8, buffer.Stride);
        Assert.Throws<ArgumentException>(() => _ = new ImageBuffer(
            2,
            2,
            ImagePixelFormat.Rgba8,
            ImageAlphaMode.Straight,
            stride: 7,
            pixels: new byte[16]));
        Assert.Throws<ArgumentException>(() => _ = new ImageBuffer(
            2,
            2,
            ImagePixelFormat.Rgba8,
            ImageAlphaMode.Straight,
            stride: 8,
            pixels: new byte[15]));
        return ValueTask.CompletedTask;
    }

    private static ValueTask StaticSequence()
    {
        ImageBuffer buffer = Rgba1x1();
        ImageSequence sequence = ImageSequence.Static(buffer);

        Assert.False(sequence.IsAnimated);
        Assert.Equal(1, sequence.Frames.Count);
        Assert.Equal(buffer, sequence.FirstFrame);
        return ValueTask.CompletedTask;
    }

    private static async ValueTask DefaultEncodeUnsupported()
    {
        var codec = new FakeImageCodec(MediaKind.Image);
        ImageSequence sequence = ImageSequence.Static(Rgba1x1());

        await Assert.ThrowsAsync<NotSupportedException>(
            () => codec.EncodeAsync(sequence, Stream.Null).AsTask()).ConfigureAwait(false);
    }

    private static ImageBuffer Rgba1x1() =>
        new(1, 1, ImagePixelFormat.Rgba8, ImageAlphaMode.Straight, 4, new byte[4]);

    private sealed class FakeImageCodec : ImageCodec
    {
        public FakeImageCodec(MediaKind kind)
            : base(new MediaCodecDescriptor(
                new MediaCodecId($"fake.image.{kind}"),
                "Fake Image",
                kind,
                MediaCodecCapabilities.Decode))
        {
        }

        public override ValueTask<MediaProbeResult> ProbeAsync(
            MediaProbeRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(MediaProbeResult.NoMatch(MediaKind.Image));

        public override ValueTask<ImageSequence> DecodeAsync(
            MediaInput input,
            ImageDecodeOptions? options = null,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ImageSequence.Static(Rgba1x1()));
    }
}

internal sealed class AssertException(string message) : Exception(message);

internal static class Assert
{
    public static void True(bool condition, string message = "Expected true.")
    {
        if (!condition)
            throw new AssertException(message);
    }

    public static void False(bool condition, string message = "Expected false.") => True(!condition, message);

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new AssertException(message ?? $"Expected <{expected}>, but was <{actual}>.");
    }

    public static void Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new AssertException($"Expected {typeof(TException).Name}, got {ex.GetType().Name}.");
        }

        throw new AssertException($"Expected {typeof(TException).Name}, but no exception was thrown.");
    }

    public static async ValueTask ThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new AssertException($"Expected {typeof(TException).Name}, got {ex.GetType().Name}.");
        }

        throw new AssertException($"Expected {typeof(TException).Name}, but no exception was thrown.");
    }
}

