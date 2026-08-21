using Broiler.Media.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// The instant a <c>reftest-wait</c> test asks to be screenshotted at, which the runner pins as
/// the render's presentation time so animated images resolve to the frame the reference
/// generator's Chromium was showing when it captured the same test.
/// </summary>
public class ScreenshotPresentationTimeTests
{
    [Fact(Timeout = 600000)]
    public void ReadsTheDelayFromTakeScreenshotDelayed()
    {
        Assert.Equal(
            TimeSpan.FromMilliseconds(300),
            WptTestRunner.ScreenshotPresentationTime(
                """
                <!DOCTYPE html>
                <html class="reftest-wait">
                <script src="/common/reftest-wait.js"></script>
                <script>
                  takeScreenshotDelayed(300);
                </script>
                <style>body { background-image: url("/images/anim-gr.gif"); }</style>
                </html>
                """));
    }

    [Theory]
    [InlineData("takeScreenshotDelayed(300)", 300)]
    [InlineData("takeScreenshotDelayed( 300 )", 300)]
    [InlineData("takeScreenshotDelayed\n(300)", 300)]
    [InlineData("takeScreenshotDelayed(1500.5)", 1500.5)]
    [InlineData("takeScreenshotDelayed(.5)", 0.5)]
    public void AcceptsTheFormsTestsActuallyWrite(string call, double expectedMilliseconds)
        => Assert.Equal(
            TimeSpan.FromMilliseconds(expectedMilliseconds),
            WptTestRunner.ScreenshotPresentationTime($"<script>{call};</script>"));

    [Theory]
    // No delayed screenshot at all: the render is of the page as loaded.
    [InlineData("<p>plain</p>")]
    // An immediate screenshot is the same instant as no screenshot.
    [InlineData("<script>takeScreenshot();</script>")]
    [InlineData("<script>takeScreenshotDelayed(0);</script>")]
    // A test's own helper whose name merely ends in the WPT one is not the WPT one.
    [InlineData("<script>myTakeScreenshotDelayed(300);</script>")]
    // A delay computed at runtime is not a literal this can read; zero is the honest answer.
    [InlineData("<script>takeScreenshotDelayed(delay);</script>")]
    public void FallsBackToZeroWhenThereIsNoLiteralDelay(string html)
        => Assert.Equal(TimeSpan.Zero, WptTestRunner.ScreenshotPresentationTime(html));

    [Fact(Timeout = 600000)]
    public void TreatsMissingMarkupAsZero()
    {
        Assert.Equal(TimeSpan.Zero, WptTestRunner.ScreenshotPresentationTime(null));
        Assert.Equal(TimeSpan.Zero, WptTestRunner.ScreenshotPresentationTime(string.Empty));
    }

    [Fact(Timeout = 600000)]
    public void TakesTheFirstDelayWhenATestSchedulesMoreThanOne()
        => Assert.Equal(
            TimeSpan.FromMilliseconds(100),
            WptTestRunner.ScreenshotPresentationTime(
                "<script>takeScreenshotDelayed(100); takeScreenshotDelayed(900);</script>"));

    /// <summary>
    /// The end the presentation time exists for: wpt's <c>images/anim-gr.gif</c> is a 10 ms green
    /// frame followed by a 100 s red one, and the four <c>css-image-animation</c> reftests
    /// screenshot at 300 ms. The 10 ms delay is below the threshold any engine honours, so the
    /// green frame occupies 100 ms — which still puts 300 ms squarely on red, the colour
    /// Chromium's reference shows.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ResolvesTheImageAnimationReftestsToTheirSecondFrame()
    {
        TimeSpan presentationTime = WptTestRunner.ScreenshotPresentationTime(
            "<script>takeScreenshotDelayed(300);</script>");

        var animation = new ImageSequence(
            [
                new ImageFrame(Rgba1x1(0, 255, 0), TimeSpan.FromMilliseconds(10)),
                new ImageFrame(Rgba1x1(255, 0, 0), TimeSpan.FromSeconds(100)),
            ],
            1,
            1,
            loopCount: 0);

        Assert.Equal(1, animation.FrameIndexAt(presentationTime));
        Assert.Equal(0, animation.FrameIndexAt(TimeSpan.Zero));
    }

    private static ImageBuffer Rgba1x1(byte r, byte g, byte b) =>
        new(1, 1, ImagePixelFormat.Rgba8, ImageAlphaMode.Straight, 4, new byte[] { r, g, b, 255 });
}
