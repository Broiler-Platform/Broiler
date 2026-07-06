using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.CheckBox;
using Broiler.UI.CheckBox.Standard;
using Broiler.UI.ImageView;
using Broiler.UI.ImageView.Standard;
using Broiler.UI.Panel;
using Broiler.UI.Panel.Standard;
using Broiler.UI.ProgressBar;
using Broiler.UI.ProgressBar.Standard;
using Broiler.UI.RadioButton;
using Broiler.UI.RadioButton.Standard;
using Broiler.UI.Slider;
using Broiler.UI.Slider.Standard;
using Broiler.UI.Standard;
using Broiler.UI.ToggleButton;
using Broiler.UI.ToggleButton.Standard;

namespace Broiler.UI.Phase5.Tests;

public sealed class Phase5ControlTests
{
    [Fact]
    public void CheckBox_Toggles_TriState_Rtl_HighContrast_Keyboard_And_Semantics()
    {
        var host = new TestHost(new BSize(220, 48));
        using UiSession session = CreateSession(host);
        var checkBox = new StandardCheckBox
        {
            Text = "Remember",
            IsThreeState = true,
            FlowDirection = UiFlowDirection.RightToLeft,
            Background = BColor.Black,
            Foreground = BColor.White,
            Accent = BColor.White,
            BorderColor = BColor.White,
        };
        session.AddRoot(checkBox);
        session.RenderFrame();
        Assert.True(checkBox.DesiredSize.Height >= 32);

        var route = new StandardInputRoute(session);
        Assert.True(route.Dispatch(Key("Space", BVirtualKey.Space, KeyboardKeyTransition.Down)));
        Assert.Equal(UiCheckState.Checked, checkBox.CheckState);
        Assert.Same(checkBox, session.FocusedElement);

        BRenderList rendered = session.RenderFrame();
        BRenderCommand.DrawText text = Assert.Single(rendered.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "Remember");
        Assert.Equal(BColor.White, text.Text.Color);

        Assert.True(route.Dispatch(Key("Space", BVirtualKey.Space, KeyboardKeyTransition.Down)));
        Assert.Equal(UiCheckState.Indeterminate, checkBox.CheckState);
        Assert.True(checkBox.GetSemanticNode().State.HasFlag(UiSemanticState.Indeterminate));

        Assert.True(route.Dispatch(MouseButtonInput(20, 20, MouseButtonTransition.Down)));
        Assert.True(route.Dispatch(MouseButtonInput(20, 20, MouseButtonTransition.Up)));
        Assert.Equal(UiCheckState.Unchecked, checkBox.CheckState);
    }

    [Fact]
    public void RadioButton_Uses_Explicit_GroupScope_And_Stays_Deterministic_Under_Reentrancy()
    {
        var host = new TestHost(new BSize(320, 96));
        using UiSession session = CreateSession(host);
        var panel = new StandardPanel { LayoutMode = UiPanelLayoutMode.Stack };
        var scope = new UiRadioGroupScope("density");
        var compact = new StandardRadioButton { Text = "Compact", GroupScope = scope };
        var roomy = new StandardRadioButton { Text = "Roomy", GroupScope = scope };
        var ungrouped = new StandardRadioButton { Text = "Ungrouped" };
        panel.AddChild(compact);
        panel.AddChild(roomy);
        panel.AddChild(ungrouped);
        session.AddRoot(panel);
        session.RenderFrame();

        compact.IsChecked = true;
        roomy.IsChecked = true;
        Assert.False(compact.IsChecked);
        Assert.True(roomy.IsChecked);

        roomy.CheckedChanged += (_, e) =>
        {
            if (e.NewValue)
                compact.IsChecked = true;
        };

        roomy.IsChecked = false;
        roomy.IsChecked = true;
        Assert.True(compact.IsChecked);
        Assert.False(roomy.IsChecked);

        ungrouped.IsChecked = true;
        Assert.True(compact.IsChecked);
        Assert.True(ungrouped.IsChecked);

        UiSemanticNode semantic = compact.GetSemanticNode();
        Assert.Equal(UiSemanticRole.RadioButton, semantic.Role);
        Assert.True(semantic.State.HasFlag(UiSemanticState.Selected));
    }

    [Fact]
    public void ToggleButton_Uses_Button_Activation_Command_And_TriState()
    {
        var host = new TestHost(new BSize(180, 48));
        using UiSession session = CreateSession(host);
        int executed = 0;
        var toggle = new StandardToggleButton
        {
            Text = "Bold",
            IsThreeState = true,
            Command = new StandardCommand("bold", () => executed++),
        };
        session.AddRoot(toggle);
        session.RenderFrame();
        var route = new StandardInputRoute(session);

        Assert.True(route.Dispatch(MouseButtonInput(10, 10, MouseButtonTransition.Down)));
        Assert.True(route.Dispatch(MouseButtonInput(10, 10, MouseButtonTransition.Up)));
        Assert.Equal(UiToggleState.On, toggle.ToggleState);
        Assert.Equal(1, executed);

        Assert.True(route.Dispatch(Key("Space", BVirtualKey.Space, KeyboardKeyTransition.Down)));
        Assert.True(route.Dispatch(Key("Space", BVirtualKey.Space, KeyboardKeyTransition.Up)));
        Assert.Equal(UiToggleState.Indeterminate, toggle.ToggleState);

        toggle.Command = new StandardCommand("blocked", () => executed++, () => false);
        toggle.Click();
        Assert.Equal(UiToggleState.Indeterminate, toggle.ToggleState);
        Assert.Equal(2, executed);
        Assert.True(toggle.GetSemanticNode().State.HasFlag(UiSemanticState.Indeterminate));
    }

    [Fact]
    public void Slider_Coerces_Steps_Pointer_Keyboard_Reversed_Direction_And_Semantics()
    {
        var host = new TestHost(new BSize(220, 48));
        using UiSession session = CreateSession(host);
        var slider = new StandardSlider
        {
            Minimum = 0,
            Maximum = 100,
            StepFrequency = 10,
        };
        session.AddRoot(slider);
        session.RenderFrame();
        var route = new StandardInputRoute(session);

        slider.Value = 52;
        Assert.Equal(50, slider.Value);

        Assert.True(route.Dispatch(MouseButtonInput(110, 24, MouseButtonTransition.Down)));
        Assert.True(route.Dispatch(MouseButtonInput(110, 24, MouseButtonTransition.Up)));
        Assert.Equal(50, slider.Value);
        Assert.Same(slider, session.FocusedElement);

        Assert.True(route.Dispatch(Key("Right", BVirtualKey.Right, KeyboardKeyTransition.Down)));
        Assert.Equal(60, slider.Value);

        slider.IsDirectionReversed = true;
        Assert.True(route.Dispatch(Key("Right", BVirtualKey.Right, KeyboardKeyTransition.Down)));
        Assert.Equal(50, slider.Value);

        slider.ValueChanged += (_, e) =>
        {
            if (e.NewValue == 70)
                slider.Value = 80;
        };
        slider.Value = 70;
        Assert.Equal(80, slider.Value);

        UiSemanticNode semantic = slider.GetSemanticNode();
        Assert.Equal(UiSemanticRole.Slider, semantic.Role);
        Assert.Equal("80", semantic.Name);
        Assert.True(semantic.State.HasFlag(UiSemanticState.Focused));
    }

    [Fact]
    public void ProgressBar_Coerces_Renders_Indeterminate_ReducedMotion_And_Direction()
    {
        var host = new TestHost(new BSize(200, 24));
        using UiSession session = CreateSession(host);
        var progress = new StandardProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 150,
            FillColor = BColor.FromArgb(0xFF, 0x12, 0x34, 0x56),
        };
        session.AddRoot(progress);
        Assert.Equal(100, progress.Value);

        progress.Value = 25;
        BRenderList determinate = session.RenderFrame();
        BRect fill = LastFillBounds(determinate, progress.FillColor);
        Assert.Equal(50, fill.Width);

        progress.IsDirectionReversed = true;
        BRenderList reversed = session.RenderFrame();
        BRect reversedFill = LastFillBounds(reversed, progress.FillColor);
        Assert.Equal(150, reversedFill.Left);

        progress.IsIndeterminate = true;
        progress.IsReducedMotion = true;
        BRect first = LastFillBounds(session.RenderFrame(), progress.FillColor);
        BRect second = LastFillBounds(session.RenderFrame(), progress.FillColor);
        Assert.Equal(first, second);
        Assert.True(progress.GetSemanticNode().State.HasFlag(UiSemanticState.Indeterminate));
    }

    [Fact]
    public void ImageView_Draws_Only_Existing_Graphics_Image_Handle()
    {
        var host = new TestHost(new BSize(128, 128));
        using UiSession session = CreateSession(host);
        var image = new StandardImageView
        {
            Image = BImageHandle.FromId(42, new BSize(64, 32)),
            AltText = "Logo",
            Stretch = UiImageStretch.Uniform,
        };
        session.AddRoot(image);

        BRenderList rendered = session.RenderFrame();
        BRenderCommand.DrawImage draw = Assert.Single(rendered.Commands.OfType<BRenderCommand.DrawImage>());
        Assert.Equal(new BRect(0, 0, 64, 32), draw.Source);
        Assert.Equal(new BRect(0, 32, 128, 64), draw.Destination);
        Assert.Equal(UiSemanticRole.ImageView, image.GetSemanticNode().Role);
        Assert.Equal("Logo", image.GetSemanticNode().Name);

        image.Image = BImageHandle.Invalid;
        Assert.Empty(session.RenderFrame().Commands.OfType<BRenderCommand.DrawImage>());
    }

    private static UiSession CreateSession(TestHost host) =>
        new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);

    private static BRect LastFillBounds(BRenderList renderList, BColor color)
    {
        for (int index = renderList.Commands.Count - 1; index >= 0; index--)
        {
            switch (renderList.Commands[index])
            {
                case BRenderCommand.FillRect fill when fill.Color == color:
                    return fill.Rect;
                case BRenderCommand.FillRoundedRect fill when fill.Color == color:
                    return fill.Rect;
            }
        }

        throw new InvalidOperationException("No fill command matched the requested color.");
    }

    private static MouseButtonEvent MouseButtonInput(double x, double y, MouseButtonTransition transition)
    {
        MouseButtons buttons = transition == MouseButtonTransition.Down ? MouseButtons.Left : MouseButtons.None;
        return new(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(x, y), buttons, MouseButton.Left, transition, InputEventSource.Synthetic);
    }

    private static KeyboardKeyEvent Key(
        string name,
        int nativeKeyCode,
        KeyboardKeyTransition transition,
        KeyboardModifierState modifiers = KeyboardModifierState.None) =>
        new(Header("keyboard"), KeyboardKey.FromName(name), transition, modifiers, nativeKeyCode, 0, 0, false, false, Source: InputEventSource.Synthetic);

    private static InputEventHeader Header(string id) =>
        new(InputDeviceId.FromOpaqueValue(id), new InputTimestamp(1, TimeSpan.TicksPerSecond, "phase5"), 1);

    private sealed class TestHost : IUiHost
    {
        public TestHost(BSize viewportSize)
        {
            ViewportSize = viewportSize;
        }

        public BSize ViewportSize { get; }

        public double Scale => 1;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }
}
