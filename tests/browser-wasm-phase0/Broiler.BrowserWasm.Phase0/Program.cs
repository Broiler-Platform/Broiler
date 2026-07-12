using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.Media.Image;
using Broiler.UI;

namespace Broiler.BrowserWasm.Phase0;

internal static class Program
{
    private const string RenderFile = "cpu-render-baseline.png";
    private const string RenderListFile = "render-list.json";
    private const string InputTraceFile = "input-trace.json";
    private const string ManifestFile = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static int Main(string[] args)
    {
        bool verify = args.Any(static argument => string.Equals(argument, "--verify", StringComparison.Ordinal));
        string baselineDirectory = ReadOption(args, "--baseline-dir") ??
            Path.Combine(FindRepositoryRoot(), "docs", "testing", "baselines", "browser-webassembly-phase0");

        GeneratedBaseline generated = GenerateBaseline();
        if (verify)
            return Verify(baselineDirectory, generated);

        Directory.CreateDirectory(baselineDirectory);
        File.WriteAllBytes(Path.Combine(baselineDirectory, RenderFile), generated.Png);
        File.WriteAllBytes(Path.Combine(baselineDirectory, RenderListFile), generated.RenderListJson);
        File.WriteAllBytes(Path.Combine(baselineDirectory, InputTraceFile), generated.InputTraceJson);
        File.WriteAllBytes(Path.Combine(baselineDirectory, ManifestFile), generated.ManifestJson);

        Console.WriteLine("Browser WebAssembly Phase 0 baselines written to " + baselineDirectory);
        PrintHashes(generated);
        return 0;
    }

    private static GeneratedBaseline GenerateBaseline()
    {
        using BImageRenderer renderer = new();
        byte[] checkerPixels =
        [
            0xFF, 0xC1, 0x07, 0xFF,
            0x16, 0x6F, 0xB7, 0xFF,
            0x16, 0x6F, 0xB7, 0xFF,
            0xFF, 0xC1, 0x07, 0xFF,
        ];
        BImageHandle checker = renderer.CreateImage(new BPixelBuffer(2, 2, checkerPixels));

        BRenderList renderList = CreateRenderList(checker);
        var descriptor = new BSurfaceDescriptor(new BSize(320, 180), 1.0, BPixelFormat.Rgba8, EnableTransparency: false);
        var frame = new BFrameContext(BColor.FromArgb(unchecked((int)0xFFF7F9FC)), 0, BRenderOptions.Default);
        using BBitmap bitmap = renderer.RenderToImage(renderList, descriptor, frame);
        byte[] rgba = bitmap.CopyRgba();
        byte[] png = bitmap.Encode(ImageEncodeFormat.Png);
        renderer.ReleaseImage(checker);

        byte[] renderListJson = JsonBytes(new
        {
            schemaVersion = 1,
            width = bitmap.Width,
            height = bitmap.Height,
            pixelFormat = "straight-alpha-rgba8",
            textIncluded = false,
            commands = renderList.Commands.Select(DescribeCommand).ToArray(),
        });
        byte[] inputTraceJson = JsonBytes(CreateInputTrace());

        string rgbaHash = Sha256(rgba);
        string pngHash = Sha256(png);
        string renderListHash = Sha256(renderListJson);
        string inputTraceHash = Sha256(inputTraceJson);
        byte[] manifestJson = JsonBytes(new
        {
            schemaVersion = 1,
            baseline = "browser-webassembly-phase0",
            captureDate = "2026-07-11",
            width = bitmap.Width,
            height = bitmap.Height,
            rgbaSha256 = rgbaHash,
            files = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RenderFile] = pngHash,
                [RenderListFile] = renderListHash,
                [InputTraceFile] = inputTraceHash,
            },
        });

        return new GeneratedBaseline(png, renderListJson, inputTraceJson, manifestJson, rgbaHash);
    }

    private static BRenderList CreateRenderList(BImageHandle checker)
    {
        var list = new BRenderList(16);
        list.FillRect(new BRect(0, 0, 320, 180), BColor.FromArgb(unchecked((int)0xFFF7F9FC)));
        list.FillRect(new BRect(12, 12, 296, 28), BColor.FromArgb(unchecked((int)0xFF16324F)));
        list.StrokeRect(new BRect(12, 12, 296, 156), BColor.FromArgb(unchecked((int)0xFF6B7C93)), 2);
        list.FillRoundedRect(new BRect(28, 58, 92, 54), BColor.FromArgb(unchecked((int)0xFFDBEAFE)), 10, 10);
        list.StrokeRoundedRect(new BRect(28, 58, 92, 54), BColor.FromArgb(unchecked((int)0xFF166FB7)), 10, 10, 2);

        // Intentionally exercise independent clip and transform stacks:
        // PopClip occurs while the translation remains active.
        list.PushClip(new BRect(142, 52, 116, 72));
        list.PushTransform(BMatrix3x2.Translation(12, 8));
        list.FillRect(new BRect(124, 62, 116, 48), BColor.FromArgb(unchecked((int)0x99F97316)));
        list.PopClip();
        list.FillRect(new BRect(252, 54, 34, 34), BColor.FromArgb(unchecked((int)0xFF16A34A)));
        list.PopTransform();

        list.DrawImage(checker, new BRect(0, 0, 2, 2), new BRect(274, 124, 24, 24), 0.85);
        list.FillRect(new BRect(28, 132, 216, 14), BColor.FromArgb(unchecked((int)0xFFCBD5E1)));
        list.FillRect(new BRect(28, 132, 132, 14), BColor.FromArgb(unchecked((int)0xFF2563EB)));
        list.Validate();
        return list;
    }

    private static object DescribeCommand(BRenderCommand command) => command switch
    {
        BRenderCommand.FillRect value => new { kind = "fillRect", rect = Rect(value.Rect), color = Color(value.Color) },
        BRenderCommand.StrokeRect value => new { kind = "strokeRect", rect = Rect(value.Rect), color = Color(value.Color), thickness = value.Thickness },
        BRenderCommand.FillRoundedRect value => new { kind = "fillRoundedRect", rect = Rect(value.Rect), color = Color(value.Color), radiusX = value.RadiusX, radiusY = value.RadiusY },
        BRenderCommand.StrokeRoundedRect value => new { kind = "strokeRoundedRect", rect = Rect(value.Rect), color = Color(value.Color), radiusX = value.RadiusX, radiusY = value.RadiusY, thickness = value.Thickness },
        BRenderCommand.DrawText value => new { kind = "drawText", text = value.Text.Text, origin = Point(value.Origin) },
        BRenderCommand.DrawImage value => new { kind = "drawImage", resource = "checker-2x2", source = Rect(value.Source), destination = Rect(value.Destination), opacity = value.Opacity },
        BRenderCommand.PushClip value => new { kind = "pushClip", rect = Rect(value.Rect) },
        BRenderCommand.PopClip => new { kind = "popClip" },
        BRenderCommand.PushTransform value => new { kind = "pushTransform", matrix = Matrix(value.Transform) },
        BRenderCommand.PopTransform => new { kind = "popTransform" },
        _ => throw new InvalidOperationException("Unknown render command: " + command.GetType().FullName),
    };

    private static object CreateInputTrace()
    {
        var pointerId = InputDeviceId.FromOpaqueValue("phase0-pointer");
        var keyboardId = InputDeviceId.FromOpaqueValue("phase0-keyboard");
        var textId = InputDeviceId.FromOpaqueValue("phase0-text");
        var events = new List<object>();

        MouseMoveEvent move = new(Header(pointerId, 1), InputPoint.ClientDeviceIndependentPixels(32, 24), MouseButtons.None);
        events.Add(Input("pointerMove", move.Header, new { x = move.Position.X, y = move.Position.Y, buttons = move.Buttons.ToString() }, UiInputEvent.FromMouseMove(move)));

        MouseButtonEvent down = new(Header(pointerId, 2), InputPoint.ClientDeviceIndependentPixels(32, 24), MouseButtons.Left, MouseButton.Left, MouseButtonTransition.Down);
        events.Add(Input("pointerButton", down.Header, new { x = down.Position.X, y = down.Position.Y, button = down.Button.ToString(), transition = down.Transition.ToString() }, UiInputEvent.FromMouseButton(down)));

        MouseMoveEvent cancelMove = new(Header(pointerId, 3), InputPoint.ClientDeviceIndependentPixels(-1, -1), MouseButtons.Left, InputEventSource.Synthetic);
        events.Add(Input("syntheticCancelMoveOutside", cancelMove.Header, new { x = cancelMove.Position.X, y = cancelMove.Position.Y, buttons = cancelMove.Buttons.ToString(), source = cancelMove.Source.ToString() }, UiInputEvent.FromMouseMove(cancelMove)));

        MouseButtonEvent cancelUp = new(Header(pointerId, 4), InputPoint.ClientDeviceIndependentPixels(-1, -1), MouseButtons.None, MouseButton.Left, MouseButtonTransition.Up, InputEventSource.Synthetic);
        events.Add(Input("syntheticCancelReleaseOutside", cancelUp.Header, new { x = cancelUp.Position.X, y = cancelUp.Position.Y, button = cancelUp.Button.ToString(), transition = cancelUp.Transition.ToString(), source = cancelUp.Source.ToString() }, UiInputEvent.FromMouseButton(cancelUp)));

        KeyboardKeyEvent keyDown = new(Header(keyboardId, 5), KeyboardKey.FromName("B"), KeyboardKeyTransition.Down, KeyboardModifierState.Control, 0, 0, 1, false, false, KeyboardKeyLocation.Standard);
        events.Add(Input("keyDown", keyDown.Header, new { key = keyDown.Key.Name, transition = keyDown.Transition.ToString(), modifiers = keyDown.Modifiers.ToString(), repeatCount = keyDown.RepeatCount, location = keyDown.Location.ToString() }, UiInputEvent.FromKeyboardKey(keyDown)));

        KeyboardKeyEvent keyUp = keyDown with { Header = Header(keyboardId, 6), Transition = KeyboardKeyTransition.Up, WasDown = true };
        events.Add(Input("keyUp", keyUp.Header, new { key = keyUp.Key.Name, transition = keyUp.Transition.ToString(), modifiers = keyUp.Modifiers.ToString(), repeatCount = keyUp.RepeatCount, location = keyUp.Location.ToString() }, UiInputEvent.FromKeyboardKey(keyUp)));

        TextInputEvent text = new(Header(textId, 7), "B");
        events.Add(Input("committedText", text.Header, new { text = text.Text }, UiInputEvent.FromTextInput(text)));

        AddComposition(events, textId, 8, "", TextCompositionState.Started, 0, 0);
        AddComposition(events, textId, 9, "に", TextCompositionState.Updated, 1, 0);
        AddComposition(events, textId, 10, "日本", TextCompositionState.Committed, 2, 0);

        MouseWheelEvent wheel = new(Header(pointerId, 11), InputPoint.ClientDeviceIndependentPixels(80, 96), MouseButtons.None, MouseWheelAxis.Vertical, -1);
        events.Add(Input("wheel", wheel.Header, new { x = wheel.Position.X, y = wheel.Position.Y, axis = wheel.Axis.ToString(), deltaNotches = wheel.DeltaNotches }, UiInputEvent.FromMouseWheel(wheel)));

        return new
        {
            schemaVersion = 1,
            coordinateSpace = "client-dip",
            purpose = "Normalized T2 input and synthetic pointer-cancel compatibility baseline",
            knownProjectionLoss = new[] { "keyboard-repeat-count", "keyboard-location", "composition-selection-start", "composition-selection-length" },
            events,
        };
    }

    private static void AddComposition(List<object> events, InputDeviceId deviceId, long sequence, string text, TextCompositionState state, int selectionStart, int selectionLength)
    {
        TextCompositionEvent composition = new(Header(deviceId, sequence), text, state, selectionStart, selectionLength);
        events.Add(Input(
            "composition" + state,
            composition.Header,
            new { text = composition.Text, state = composition.State.ToString(), selectionStart = composition.SelectionStart, selectionLength = composition.SelectionLength },
            UiInputEvent.FromTextComposition(composition)));
    }

    private static object Input(string kind, InputEventHeader header, object source, UiInputEvent projected) => new
    {
        kind,
        sequence = header.SequenceNumber,
        timestampTicks = header.Timestamp.Ticks,
        deviceId = header.DeviceId.Value,
        source,
        uiProjection = new
        {
            kind = projected.Kind.ToString(),
            x = projected.Position.X,
            y = projected.Position.Y,
            projected.Text,
            projected.KeyName,
            mouseButton = projected.MouseButton.ToString(),
            mouseTransition = projected.MouseButtonTransition?.ToString(),
            keyTransition = projected.KeyTransition?.ToString(),
            modifiers = projected.KeyModifiers.ToString(),
            compositionState = projected.CompositionState?.ToString(),
            wheelAxis = projected.WheelAxis.ToString(),
            projected.WheelDeltaNotches,
        },
    };

    private static InputEventHeader Header(InputDeviceId deviceId, long sequence) =>
        new(deviceId, new InputTimestamp(sequence * 100, 1000, "phase0-clock"), sequence);

    private static object Rect(BRect rect) => new { x = rect.X, y = rect.Y, width = rect.Width, height = rect.Height };

    private static object Point(BPoint point) => new { x = point.X, y = point.Y };

    private static object Matrix(BMatrix3x2 matrix) => new { matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32 };

    private static string Color(BColor color) => string.Format(
        CultureInfo.InvariantCulture,
        "#{0:X2}{1:X2}{2:X2}{3:X2}",
        color.R,
        color.G,
        color.B,
        color.A);

    private static int Verify(string directory, GeneratedBaseline expected)
    {
        if (!Directory.Exists(directory))
        {
            Console.Error.WriteLine("Baseline directory does not exist: " + directory);
            return 1;
        }

        var expectedFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [RenderFile] = expected.Png,
            [RenderListFile] = expected.RenderListJson,
            [InputTraceFile] = expected.InputTraceJson,
            [ManifestFile] = expected.ManifestJson,
        };

        bool valid = true;
        foreach ((string fileName, byte[] expectedBytes) in expectedFiles)
        {
            string path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine("Missing baseline: " + path);
                valid = false;
                continue;
            }

            byte[] actualBytes = File.ReadAllBytes(path);
            if (!actualBytes.AsSpan().SequenceEqual(expectedBytes))
            {
                Console.Error.WriteLine(
                    fileName + " differs. expected=" + Sha256(expectedBytes) + " actual=" + Sha256(actualBytes));
                valid = false;
            }
        }

        if (!valid)
            return 1;

        Console.WriteLine("Browser WebAssembly Phase 0 baselines verified: " + directory);
        PrintHashes(expected);
        return 0;
    }

    private static void PrintHashes(GeneratedBaseline baseline)
    {
        Console.WriteLine("  rgba: " + baseline.RgbaSha256);
        Console.WriteLine("  " + RenderFile + ": " + Sha256(baseline.Png));
        Console.WriteLine("  " + RenderListFile + ": " + Sha256(baseline.RenderListJson));
        Console.WriteLine("  " + InputTraceFile + ": " + Sha256(baseline.InputTraceJson));
        Console.WriteLine("  " + ManifestFile + ": " + Sha256(baseline.ManifestJson));
    }

    private static byte[] JsonBytes(object value)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
    }

    private static string Sha256(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string? ReadOption(string[] args, string name)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
                return Path.GetFullPath(args[index + 1]);
        }

        return null;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Broiler.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from " + AppContext.BaseDirectory);
    }

    private sealed record GeneratedBaseline(
        byte[] Png,
        byte[] RenderListJson,
        byte[] InputTraceJson,
        byte[] ManifestJson,
        string RgbaSha256);
}
