using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Broiler.Media;
using Broiler.Media.Image;
using Gfx = Broiler.Graphics;
using Spec = Broiler.Media.Image.Managed.Tests.PngFormatBuilder.ApngFrameSpec;

namespace Broiler.Media.Image.Managed.Tests;

internal static class Program
{
    private const string ProgressiveGradientBase64 =
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wCEAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDIBCQkJDAsMGA0NGDIhHCEyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMv/CABEIAEAAYAMBIgACEQEDEQH/xABXAAEBAQAAAAAAAAAAAAAAAAADBgIQAQADAAAAAAAAAAAAAAAAAAABA2EBAQEBAQEAAAAAAAAAAAAAAAIEBQYBEQEBAQEBAAAAAAAAAAAAAAAAAgERQP/aAAwDAQACEQMRAAABhUZPCKMjoFGRUgjo6QR9qgUZHTPIyZ3DijI6BRkVIoyOkUZFQKMjpnkZM/hx26KgEdHSCOipBGR0CjIqZ5GTP4cUZFQKMjpFGRUijI6BRkVP/9oADAMBAAIRAxEAABAIELDfz704w8ABPDL37y8wIEL/2gAIAQEAAT8BitFaK0VorRWitFaK0VorRWitFaK0VorRWitFaK0VorRWitFaK0VorRWitFaK0VorRWitFaK0VorRWitFaK0VorRWitFaK0VorRWitFaK0VorRWitFaK0VorRWitFaK0VorRWitFaK0VorRWitFaK0VorRWitFaK0VorRWitFaK0VorRW/9oACAECEQE/Aeuuuuuuuuuuuuuuuuuuuuuuuv/aAAgBAxEBPwGbTabTabTabTabTabTabTabTabTabTabTabTb/2gAIAQEAAT8QyZMmTJkyZMmTJkyZMmTJkyZMmTJkyZMmTJkyZMmTJkyZMmTJkyZMmTJkyZMmTJkyZMmTJkyZMmTJkyZMmTJkyZMmTJkyZMmTJkyZMmTJkyZMmTJk/9oACAECEQE/EMMMMMMMMMMMMMMMMMMMMMMMMP/aAAgBAxEBPxD0f/8A/wD/AP8A/wD/AP/Z";

    private static async Task<int> Main()
    {
        var tests = new List<(string Name, Func<ValueTask> Body)>
        {
            ("Managed image provider exposes PNG, JPEG and BMP codecs", ProviderExposesCodecs),
            ("Catalog selects concrete managed codecs by signature", CatalogSelectsBySignature),
            ("PNG encode bytes match Graphics baseline", PngEncodeMatchesGraphics),
            ("BMP encode bytes match Graphics baseline", BmpEncodeMatchesGraphics),
            ("JPEG encode bytes match Graphics baseline", JpegEncodeMatchesGraphics),
            ("PNG grayscale, palette, tRNS and Adam7 decode match Graphics", PngVariantsDecodeLikeGraphics),
            ("APNG blend/dispose/timing decode matches Graphics", ApngDecodeMatchesGraphics),
            ("APNG encode bytes and decoded frames match Graphics", ApngEncodeMatchesGraphics),
            ("Progressive JPEG fixture decodes like Graphics", ProgressiveJpegMatchesGraphics),
            ("Malformed PNG CRC is rejected", MalformedPngRejected),
            ("Encoded input limit is enforced", EncodedInputLimitEnforced),
            ("Still codecs reject animated encode", StillCodecsRejectAnimatedEncode),
            ("Media.Image.Managed runtime has no Graphics dependency", RuntimeHasNoGraphicsReference),
        };

        int passed = 0;
        var failures = new List<string>();
        Console.WriteLine($"Running {tests.Count} managed image codec test(s)...\n");

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

    private static ValueTask ProviderExposesCodecs()
    {
        IReadOnlyList<ImageCodec> codecs = ManagedImageCodecs.CreateCodecs();
        Assert.Equal(3, codecs.Count);
        Assert.True(codecs[0] is PngImageCodec, "PNG should be the first managed image codec.");
        Assert.True(codecs[1] is JpegImageCodec, "JPEG should be the second managed image codec.");
        Assert.True(codecs[2] is BmpImageCodec, "BMP should be the third managed image codec.");
        return ValueTask.CompletedTask;
    }

    private static async ValueTask CatalogSelectsBySignature()
    {
        var catalog = new MediaCodecCatalog(ManagedImageCodecs.CreateCodecs());
        ImageBuffer src = MakeGradient(8, 8);
        byte[] png = new PngImageCodec().Encode(src);
        byte[] jpeg = new JpegImageCodec().Encode(src, quality: 90);
        byte[] bmp = new BmpImageCodec().Encode(src);

        Assert.True((await SelectAsync(catalog, png).ConfigureAwait(false))?.Codec is PngImageCodec);
        Assert.True((await SelectAsync(catalog, jpeg).ConfigureAwait(false))?.Codec is JpegImageCodec);
        Assert.True((await SelectAsync(catalog, bmp).ConfigureAwait(false))?.Codec is BmpImageCodec);
        Assert.True(await SelectAsync(catalog, [0, 1, 2, 3]).ConfigureAwait(false) is null);
    }

    private static ValueTask PngEncodeMatchesGraphics()
    {
        ImageBuffer src = MakeGradient(37, 19);
        byte[] media = new PngImageCodec().Encode(src);
        byte[] graphics = Gfx.ManagedImageCodec.Instance.Encode(ToGraphics(src), Gfx.BImageEncodeFormat.Png);

        Assert.BytesEqual(graphics, media, "PNG encoder bytes should match the Graphics baseline.");
        CompareStillDecode(media);
        return ValueTask.CompletedTask;
    }

    private static ValueTask BmpEncodeMatchesGraphics()
    {
        ImageBuffer src = MakeGradient(40, 24);
        byte[] media = new BmpImageCodec().Encode(src);
        byte[] graphics = Gfx.ManagedImageCodec.Instance.Encode(ToGraphics(src), Gfx.BImageEncodeFormat.Bmp);

        Assert.BytesEqual(graphics, media, "BMP encoder bytes should match the Graphics baseline.");
        CompareStillDecode(media);
        return ValueTask.CompletedTask;
    }

    private static ValueTask JpegEncodeMatchesGraphics()
    {
        ImageBuffer src = MakeGradient(64, 48);
        byte[] media = new JpegImageCodec().Encode(src, quality: 90);
        byte[] graphics = Gfx.ManagedImageCodec.Instance.Encode(ToGraphics(src), Gfx.BImageEncodeFormat.Jpeg, quality: 90);

        Assert.BytesEqual(graphics, media, "JPEG encoder bytes should match the Graphics baseline.");
        CompareStillDecode(media);
        return ValueTask.CompletedTask;
    }

    private static ValueTask PngVariantsDecodeLikeGraphics()
    {
        var fixtures = new List<byte[]>
        {
            PngFormatBuilder.Build(2, 2, 8, 0, [[0, 128], [255, 64]]),
            PngFormatBuilder.Build(8, 1, 1, 0, [[0xA1]]),
            PngFormatBuilder.Build(3, 1, 8, 3, [[0, 1, 2]], [10, 20, 30, 40, 50, 60, 70, 80, 90], [0, 128]),
            PngFormatBuilder.Build(2, 1, 8, 4, [[100, 50, 200, 255]]),
            PngFormatBuilder.Build(2, 1, 8, 2, [[255, 0, 255, 1, 2, 3]], trns: [0, 255, 0, 0, 0, 255]),
            PngFormatBuilder.Build(1, 1, 16, 2, [[0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC]]),
        };

        ImageBuffer adam7 = MakeGradient(33, 21);
        fixtures.Add(PngFormatBuilder.BuildInterlacedRgba(adam7.Width, adam7.Height, adam7.Rgba));

        foreach (byte[] fixture in fixtures)
            CompareStillDecode(fixture);

        return ValueTask.CompletedTask;
    }

    private static ValueTask ApngDecodeMatchesGraphics()
    {
        var cases = new List<IReadOnlyList<Spec>>
        {
            new Spec[]
            {
                new(4, 4, 0, 0, 1, 10, 0, 0, Fill(4, 4, 255, 0, 0, 255)),
                new(2, 2, 1, 1, 1, 10, 0, 0, Fill(2, 2, 0, 0, 255, 255)),
            },
            new Spec[]
            {
                new(2, 2, 0, 0, 1, 10, 0, 0, Fill(2, 2, 255, 0, 0, 255)),
                new(2, 2, 0, 0, 1, 10, 0, 1, Fill(2, 2, 0, 255, 0, 128)),
            },
            new Spec[]
            {
                new(4, 4, 0, 0, 1, 10, 0, 0, Fill(4, 4, 255, 0, 0, 255)),
                new(2, 2, 0, 0, 2, 10, 1, 0, Fill(2, 2, 0, 0, 255, 255)),
                new(2, 2, 2, 2, 3, 0, 0, 0, Fill(2, 2, 0, 255, 0, 255)),
            },
            new Spec[]
            {
                new(4, 4, 0, 0, 1, 10, 0, 0, Fill(4, 4, 255, 0, 0, 255)),
                new(2, 2, 1, 1, 2, 10, 2, 0, Fill(2, 2, 0, 0, 255, 255)),
                new(1, 1, 0, 0, 3, 0, 0, 0, Fill(1, 1, 0, 255, 0, 255)),
            },
        };

        foreach (IReadOnlyList<Spec> frames in cases)
            CompareAnimationDecode(PngFormatBuilder.BuildApng(4, 4, numPlays: 5, frames));

        return ValueTask.CompletedTask;
    }

    private static ValueTask ApngEncodeMatchesGraphics()
    {
        ImageSequence mediaSequence = MakeSequence(6, 5, loop: 3, (1, 10), (2, 10), (5, 100));
        Gfx.BImageSequence graphicsSequence = ToGraphics(mediaSequence);

        byte[] media = new PngImageCodec().EncodeAnimation(mediaSequence);
        byte[] graphics = Gfx.ManagedImageCodec.Instance.EncodeAnimation(graphicsSequence);

        Assert.BytesEqual(graphics, media, "APNG encoder bytes should match the Graphics baseline.");
        CompareAnimationDecode(media);
        return ValueTask.CompletedTask;
    }

    private static ValueTask ProgressiveJpegMatchesGraphics()
    {
        byte[] jpeg = Convert.FromBase64String(ProgressiveGradientBase64);
        CompareStillDecode(jpeg);
        return ValueTask.CompletedTask;
    }

    private static ValueTask MalformedPngRejected()
    {
        byte[] png = new PngImageCodec().Encode(MakeGradient(8, 8));
        png[png.Length - 6] ^= 0xFF;

        Assert.Throws<FormatException>(() => new PngImageCodec().Decode(png));
        return ValueTask.CompletedTask;
    }

    private static async ValueTask EncodedInputLimitEnforced()
    {
        byte[] png = new PngImageCodec().Encode(MakeGradient(8, 8));
        using var input = new MediaInput(new MemoryStream(png), leaveOpen: false);
        var options = new ImageDecodeOptions(new MediaLimits(maxEncodedBytes: png.Length - 1));

        await Assert.ThrowsAsync<MediaException>(
            () => new PngImageCodec().DecodeAsync(input, options).AsTask()).ConfigureAwait(false);
    }

    private static async ValueTask StillCodecsRejectAnimatedEncode()
    {
        ImageSequence animated = MakeSequence(2, 2, loop: 1, (1, 10), (1, 10));

        await Assert.ThrowsAsync<NotSupportedException>(
            () => new JpegImageCodec().EncodeAsync(animated, Stream.Null, new ImageEncodeOptions(ImageEncodeFormat.Jpeg)).AsTask()).ConfigureAwait(false);
        await Assert.ThrowsAsync<NotSupportedException>(
            () => new BmpImageCodec().EncodeAsync(animated, Stream.Null, new ImageEncodeOptions(ImageEncodeFormat.Bmp)).AsTask()).ConfigureAwait(false);
    }

    private static ValueTask RuntimeHasNoGraphicsReference()
    {
        string root = FindMediaRoot();
        string runtimeRoot = Path.Combine(root, "Broiler.Media.Image.Managed");
        foreach (string file in Directory.EnumerateFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            Assert.DoesNotContain("Broiler.Graphics", text, file);
            Assert.DoesNotContain("Gfx.", text, file);
        }

        return ValueTask.CompletedTask;
    }

    private static async ValueTask<MediaCodecMatch?> SelectAsync(MediaCodecCatalog catalog, byte[] data)
    {
        using var input = new MediaInput(new MemoryStream(data), leaveOpen: false);
        return await catalog.SelectAsync(MediaKind.Image, input).ConfigureAwait(false);
    }

    private static void CompareStillDecode(byte[] encoded)
    {
        Gfx.BPixelBuffer oldPixels = Gfx.ManagedImageCodec.Instance.Decode(encoded);

        var catalog = new MediaCodecCatalog(ManagedImageCodecs.CreateCodecs());
        using var input = new MediaInput(new MemoryStream(encoded), leaveOpen: false);
        ImageCodec codec = (ImageCodec)catalog.SelectAsync(MediaKind.Image, input).AsTask().GetAwaiter().GetResult()!.Codec;

        using var decodeInput = new MediaInput(new MemoryStream(encoded), leaveOpen: false);
        ImageSequence sequence = codec.DecodeAsync(decodeInput).AsTask().GetAwaiter().GetResult();

        Assert.False(sequence.IsAnimated, "Still decode should produce a single frame.");
        ComparePixels(oldPixels, sequence.FirstFrame);
    }

    private static void CompareAnimationDecode(byte[] encoded)
    {
        Gfx.BImageSequence oldSequence = Gfx.ManagedImageCodec.Instance.DecodeAnimation(encoded);
        ImageSequence newSequence = new PngImageCodec().DecodeAnimation(encoded);

        Assert.Equal(oldSequence.Width, newSequence.Width, "animation width");
        Assert.Equal(oldSequence.Height, newSequence.Height, "animation height");
        Assert.Equal(oldSequence.LoopCount, newSequence.LoopCount, "animation loop count");
        Assert.Equal(oldSequence.Frames.Count, newSequence.Frames.Count, "animation frame count");

        for (int i = 0; i < oldSequence.Frames.Count; i++)
        {
            Gfx.BImageFrame oldFrame = oldSequence.Frames[i];
            ImageFrame newFrame = newSequence.Frames[i];
            Assert.Equal(oldFrame.DelayNumerator, newFrame.DelayNumerator, $"frame {i} delay numerator");
            Assert.Equal(oldFrame.DelayDenominator, newFrame.DelayDenominator, $"frame {i} delay denominator");
            ComparePixels(oldFrame.Pixels, newFrame.Pixels);
        }
    }

    private static void ComparePixels(Gfx.BPixelBuffer expected, ImageBuffer actual)
    {
        Assert.Equal(expected.Width, actual.Width, "width");
        Assert.Equal(expected.Height, actual.Height, "height");
        Assert.Equal(expected.Rgba.Length, actual.Rgba.Length, "rgba length");
        Assert.BytesEqual(expected.Rgba, actual.Rgba, "rgba");
    }

    private static ImageBuffer MakeGradient(int width, int height)
    {
        byte[] rgba = new byte[width * height * 4];
        int i = 0;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            rgba[i++] = (byte)(x * 7 + 1);
            rgba[i++] = (byte)(y * 11 + 2);
            rgba[i++] = (byte)((x ^ y) * 13 + 3);
            rgba[i++] = (byte)(255 - ((x + y) & 0xFF));
        }

        return new ImageBuffer(width, height, rgba);
    }

    private static ImageSequence MakeSequence(int width, int height, int loop, params (int Numerator, int Denominator)[] delays)
    {
        var frames = new List<ImageFrame>();
        for (int frameIndex = 0; frameIndex < delays.Length; frameIndex++)
        {
            byte[] rgba = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                rgba[i * 4] = (byte)(i * 3 + frameIndex * 40);
                rgba[i * 4 + 1] = (byte)(i * 5 + frameIndex * 20);
                rgba[i * 4 + 2] = (byte)(frameIndex * 60 + i);
                rgba[i * 4 + 3] = (byte)(200 + frameIndex * 10);
            }

            frames.Add(new ImageFrame(new ImageBuffer(width, height, rgba), delays[frameIndex].Numerator, delays[frameIndex].Denominator));
        }

        return new ImageSequence(frames, width, height, loop);
    }

    private static Gfx.BPixelBuffer ToGraphics(ImageBuffer buffer) =>
        new(buffer.Width, buffer.Height, (byte[])buffer.Rgba.Clone());

    private static Gfx.BImageSequence ToGraphics(ImageSequence sequence)
    {
        var frames = new List<Gfx.BImageFrame>(sequence.Frames.Count);
        foreach (ImageFrame frame in sequence.Frames)
            frames.Add(new Gfx.BImageFrame(ToGraphics(frame.Pixels), frame.DelayNumerator, frame.DelayDenominator));

        return new Gfx.BImageSequence(frames, sequence.Width, sequence.Height, sequence.LoopCount);
    }

    private static byte[] Fill(int width, int height, byte r, byte g, byte b, byte a)
    {
        byte[] rgba = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
        {
            rgba[i * 4] = r;
            rgba[i * 4 + 1] = g;
            rgba[i * 4 + 2] = b;
            rgba[i * 4 + 3] = a;
        }

        return rgba;
    }

    private static string FindMediaRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Broiler.Media.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Broiler.Media component root.");
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

    public static void BytesEqual(IReadOnlyList<byte> expected, IReadOnlyList<byte> actual, string? message = null)
    {
        if (expected.Count != actual.Count)
            throw new AssertException(message ?? $"Expected {expected.Count} byte(s), got {actual.Count}.");

        for (int i = 0; i < expected.Count; i++)
        {
            if (expected[i] != actual[i])
                throw new AssertException(message ?? $"Byte {i} differs: expected {expected[i]}, got {actual[i]}.");
        }
    }

    public static void DoesNotContain(string unexpected, string actual, string? message = null)
    {
        if (actual.Contains(unexpected, StringComparison.Ordinal))
            throw new AssertException(message ?? $"Expected text not to contain '{unexpected}'.");
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
