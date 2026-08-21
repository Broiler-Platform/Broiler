using Broiler.Code.Core.Hosting;
using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI;

namespace Broiler.Code.Core.Tests;

/// <summary>
/// The evdev translation the Linux head depends on.
///
/// evdev reports device motion, not cursor position, and carries no characters
/// at all. Everything a windowing input stack would have done has to happen in
/// the router, which is why it lives in Core and is tested here rather than
/// being written into a head nobody can run from this machine.
/// </summary>
public sealed class EvdevInputRouterTests
{
    private static readonly BSize Viewport = new(800, 600);

    [Fact(Timeout = 600000)]
    public void The_Pointer_Starts_Centred_And_Accumulates_Relative_Motion()
    {
        var router = new EvdevInputRouter(Viewport);
        router.SetViewport(Viewport);

        Assert.Equal(400, router.PointerPosition.X);
        Assert.Equal(300, router.PointerPosition.Y);

        router.FromMouseMove(Move(10, -20));

        Assert.Equal(410, router.PointerPosition.X);
        Assert.Equal(280, router.PointerPosition.Y);
    }

    [Fact(Timeout = 600000)]
    public void Motion_Is_Clamped_To_The_Viewport()
    {
        var router = new EvdevInputRouter(Viewport);
        router.SetViewport(Viewport);

        // A device can report motion forever. Without a clamp the pointer walks
        // off the window and every hit test misses.
        router.FromMouseMove(Move(10_000, 10_000));
        Assert.Equal(799, router.PointerPosition.X);
        Assert.Equal(599, router.PointerPosition.Y);

        router.FromMouseMove(Move(-10_000, -10_000));
        Assert.Equal(0, router.PointerPosition.X);
        Assert.Equal(0, router.PointerPosition.Y);
    }

    [Fact(Timeout = 600000)]
    public void A_Move_Carries_The_Tracked_Position_Not_The_Delta()
    {
        var router = new EvdevInputRouter(Viewport);
        router.SetViewport(Viewport);

        UiInputEvent moved = router.FromMouseMove(Move(5, 5));

        Assert.Equal(405, moved.Position.X);
        Assert.Equal(305, moved.Position.Y);
    }

    [Fact(Timeout = 600000)]
    public void A_Button_Press_Reports_Where_The_Pointer_Is()
    {
        var router = new EvdevInputRouter(Viewport);
        router.SetViewport(Viewport);
        router.FromMouseMove(Move(-100, 50));

        UiInputEvent pressed = router.FromMouseButton(Button(MouseButtonTransition.Down));

        // evdev button events carry no position at all; without this a click
        // would always land wherever the event happened to default to.
        Assert.Equal(300, pressed.Position.X);
        Assert.Equal(350, pressed.Position.Y);
    }

    [Fact(Timeout = 600000)]
    public void Shrinking_The_Viewport_Clamps_Rather_Than_Recentres()
    {
        var router = new EvdevInputRouter(Viewport);
        router.SetViewport(Viewport);
        router.FromMouseMove(Move(300, 200));
        Assert.Equal(700, router.PointerPosition.X);

        router.SetViewport(new BSize(400, 600));

        // Recentring on resize would move the cursor out from under the hand.
        Assert.Equal(399, router.PointerPosition.X);
        Assert.Equal(500, router.PointerPosition.Y);
    }

    [Fact(Timeout = 600000)]
    public void An_Absolute_Position_From_X11_Wins_And_Skips_Sub_Pixel_Noise()
    {
        var router = new EvdevInputRouter(Viewport);
        router.SetViewport(Viewport);

        InputDeviceId device = InputDeviceId.FromOpaqueValue("x11");
        Assert.NotNull(router.SetAbsolutePointer(120, 90, device, 1));
        Assert.Equal(120, router.PointerPosition.X);

        // At 60 Hz a queue of moves nobody can see is a queue nobody needs.
        Assert.Null(router.SetAbsolutePointer(120.2, 90.1, device, 2));
        Assert.NotNull(router.SetAbsolutePointer(130, 90, device, 3));
    }

    [Theory]
    [InlineData("KeyA", "A")]
    [InlineData("KeyZ", "Z")]
    [InlineData("Digit7", "7")]
    [InlineData("ArrowLeft", "ArrowLeft")]
    [InlineData("Enter", "Enter")]
    public void Key_Names_Are_Normalised_To_What_The_Controls_Switch_On(string given, string expected) =>
        Assert.Equal(expected, EvdevInputRouter.NormalizeKeyName(given));

    [Theory]
    [InlineData("KeyA", false, "a")]
    [InlineData("KeyA", true, "A")]
    [InlineData("Digit1", false, "1")]
    [InlineData("Digit1", true, "!")]
    [InlineData("Space", false, " ")]
    [InlineData("Slash", true, "?")]
    [InlineData("Period", false, ".")]
    public void A_Key_Produces_The_Character_A_Us_Layout_Would(
        string key, bool shift, string expected)
    {
        var router = new EvdevInputRouter(Viewport);

        (_, UiInputEvent? text) = router.FromKey(Key(key, KeyboardKeyTransition.Down,
            shift ? KeyboardModifierState.Shift : KeyboardModifierState.None));

        Assert.NotNull(text);
        Assert.Equal(expected, text.Text);
    }

    [Fact(Timeout = 600000)]
    public void A_Shortcut_Produces_A_Key_But_No_Text()
    {
        var router = new EvdevInputRouter(Viewport);

        // Ctrl+S must reach the shell as a key, and must not insert an "s".
        (UiInputEvent key, UiInputEvent? text) = router.FromKey(
            Key("KeyS", KeyboardKeyTransition.Down, KeyboardModifierState.Control));

        Assert.Equal(UiInputEventKind.KeyboardKey, key.Kind);
        Assert.Null(text);
    }

    [Fact(Timeout = 600000)]
    public void A_Key_Release_Produces_No_Text()
    {
        var router = new EvdevInputRouter(Viewport);

        (_, UiInputEvent? text) = router.FromKey(
            Key("KeyA", KeyboardKeyTransition.Up, KeyboardModifierState.None));

        // Otherwise every character is typed twice.
        Assert.Null(text);
    }

    [Fact(Timeout = 600000)]
    public void A_Key_With_No_Character_Produces_Only_A_Key()
    {
        var router = new EvdevInputRouter(Viewport);

        (UiInputEvent key, UiInputEvent? text) = router.FromKey(
            Key("ArrowLeft", KeyboardKeyTransition.Down, KeyboardModifierState.None));

        Assert.Equal("ArrowLeft", key.KeyName);
        Assert.Null(text);
    }

    private static MouseMoveEvent Move(double deltaX, double deltaY) => new(
        Header("mouse"),
        InputPoint.ClientDeviceIndependentPixels(deltaX, deltaY),
        MouseButtons.None,
        InputEventSource.Raw);

    private static MouseButtonEvent Button(MouseButtonTransition transition) => new(
        Header("mouse"),
        InputPoint.ClientDeviceIndependentPixels(0, 0),
        transition == MouseButtonTransition.Down ? MouseButtons.Left : MouseButtons.None,
        MouseButton.Left,
        transition,
        InputEventSource.Raw);

    private static KeyboardKeyEvent Key(
        string name, KeyboardKeyTransition transition, KeyboardModifierState modifiers) => new(
        Header("keyboard"),
        KeyboardKey.FromName(name),
        transition,
        modifiers,
        0,
        0,
        0,
        false,
        false,
        Source: InputEventSource.Raw);

    private static InputEventHeader Header(string device) => new(
        InputDeviceId.FromOpaqueValue(device),
        new InputTimestamp(1, TimeSpan.TicksPerSecond, "evdev-test"),
        1);
}
