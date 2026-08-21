namespace Broiler.Cli.Tests;

/// <summary>
/// The single-API platform surfaces the privacy-test-page comparison found missing on the
/// fingerprinting page (issue #1755): <c>Notification</c>, <c>MediaSource</c>,
/// <c>canPlayType</c>, <c>performance.memory</c>/<c>console.memory</c>,
/// <c>PerformanceNavigationTiming.nextHopProtocol</c>, <c>screen.orientation</c>, and the
/// <c>navigator</c> capability members.
///
/// Each was previously a <c>ReferenceError</c> or a TypeError on <c>undefined</c> — which aborts
/// the whole function that asked, not just the read — so what these tests pin is first that the
/// name resolves and second that the answer is the one the engine can honestly give. Several of
/// those answers are negative (no plugins, no gamepads, no playable media type); a negative answer
/// and a missing one are what this change is about telling apart, so the negatives are asserted as
/// precisely as the positives.
/// </summary>
public class PlatformCapabilitySurfaceTests
{
    private static string Run(string script, string url = "https://example.test/page.html")
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
" + script + @"
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, url);
    }

    [Fact(Timeout = 600000)]
    public void Notification_Reports_A_Settled_Denied_Permission()
    {
        var result = Run(@"
r.push(typeof Notification === 'function');
r.push(Notification.permission === 'denied');
// requestPermission resolves — it does not reject — with the same terminal answer, by promise
// and by the legacy callback alike.
Notification.requestPermission(function (p) { r.push('cb:' + p); });
r.push(typeof Notification.requestPermission().then === 'function');
// Constructing one does not throw, so a handler that shows a notification keeps its remaining
// statements.
var n = new Notification('title', { body: 'body', tag: 't' });
r.push(n.title === 'title');
r.push(n.body === 'body');
r.push(typeof n.close === 'function');
");
        Assert.Contains("true,true,cb:denied,true,true,true,true", result);
    }

    [Fact(Timeout = 600000)]
    public void Media_Type_Support_Answers_No_Rather_Than_Throwing()
    {
        var result = Run(@"
var video = document.createElement('video');
var audio = document.createElement('audio');
// The empty string is canPlayType's specified 'cannot be rendered', not a missing answer.
r.push(video.canPlayType('video/mp4; codecs=""avc1.42E01E""') === '');
r.push(audio.canPlayType('audio/mpeg') === '');
// The member belongs to HTMLMediaElement, so it must not appear on other elements.
r.push(!('canPlayType' in document.createElement('div')));
r.push(typeof MediaSource === 'function');
r.push(MediaSource.isTypeSupported('video/webm; codecs=""vp9""') === false);
var source = new MediaSource();
r.push(source.readyState === 'closed');
try {
  source.addSourceBuffer('video/webm; codecs=""vp9""');
  r.push('no-throw');
} catch (e) {
  r.push('name:' + e.name);
}
");
        Assert.Contains("true,true,true,true,true,true,name:NotSupportedError", result);
    }

    [Fact(Timeout = 600000)]
    public void Memory_Info_Is_One_Object_On_Performance_And_Console()
    {
        var result = Run(@"
r.push(performance.memory === console.memory);
var m = performance.memory;
r.push(typeof m.jsHeapSizeLimit === 'number');
r.push(typeof m.totalJSHeapSize === 'number');
r.push(typeof m.usedJSHeapSize === 'number');
r.push(m.usedJSHeapSize > 0);
// A heap cannot hold more than it has committed, and cannot have committed more than the limit.
r.push(m.usedJSHeapSize <= m.totalJSHeapSize);
r.push(m.totalJSHeapSize <= m.jsHeapSizeLimit);
// Reported at 100 KiB granularity, so the low bits carry no entropy to fingerprint with.
r.push(m.usedJSHeapSize % (100 * 1024) === 0);
");
        Assert.Contains("true,true,true,true,true,true,true,true", result);
    }

    [Fact(Timeout = 600000)]
    public void Navigation_Timing_Entry_Names_The_Protocol_It_Was_Fetched_Over()
    {
        var result = Run(@"
var entries = performance.getEntries();
r.push(entries.length === 1);
var nav = entries[0];
r.push(nav.entryType === 'navigation');
r.push(nav.type === 'navigate');
r.push(nav.name === 'https://example.test/page.html');
r.push(nav.nextHopProtocol === 'http/1.1');
r.push(nav.startTime === 0);
r.push(performance.getEntriesByType('navigation')[0] === nav);
r.push(performance.getEntriesByType('resource').length === 0);
r.push(performance.getEntriesByName('https://example.test/page.html')[0] === nav);
r.push(performance.getEntriesByName('https://other.test/').length === 0);
");
        Assert.Contains("true,true,true,true,true,true,true,true,true,true", result);
    }

    [Fact(Timeout = 600000)]
    public void Navigation_Timing_Names_No_Protocol_Without_A_Network_Hop()
    {
        // A file: document had no next hop, and the specification's answer for that is the empty
        // string — not the token the HTTP loader would have used.
        var result = Run("r.push(performance.getEntries()[0].nextHopProtocol === '');", "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact(Timeout = 600000)]
    public void Screen_Orientation_Follows_The_Screen_Shape()
    {
        var result = Run(@"
var o = screen.orientation;
r.push(o.angle === 0);
r.push(o.type === (screen.width > screen.height ? 'landscape-primary' : 'portrait-primary'));
r.push(o.onchange === null);
o.onchange = function () {};
r.push(typeof o.onchange === 'function');
");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact(Timeout = 600000)]
    public void Navigator_Capability_Members_Answer_Negatively_Without_Throwing()
    {
        var result = Run(@"
r.push(navigator.javaEnabled() === false);
// The empty-plugin branch of HTML's plugin surface: a page may iterate it and find nothing,
// where before Array.from threw on undefined.
r.push(Array.from(navigator.plugins).length === 0);
r.push(Array.from(navigator.mimeTypes).length === 0);
r.push(navigator.pdfViewerEnabled === false);
r.push(navigator.plugins.item(0) === null);
r.push(Array.from(navigator.getGamepads()).length === 0);
// The legacy quota interface reports the quota-managed storage Broiler has none of.
navigator.webkitTemporaryStorage.queryUsageAndQuota(function (usage, quota) {
  r.push('temp:' + usage + '/' + quota);
});
navigator.webkitPersistentStorage.queryUsageAndQuota(function (usage, quota) {
  r.push('persist:' + usage + '/' + quota);
});
");
        Assert.Contains("true,true,true,true,true,true,temp:0/0,persist:0/0", result);
    }

    [Fact(Timeout = 600000)]
    public void GetBattery_Resolves_With_An_Externally_Powered_System()
    {
        var result = Run(@"
var p = navigator.getBattery();
r.push(typeof p.then === 'function');
p.then(function (b) {
  r.push(b.charging === true);
  r.push(b.level === 1);
  r.push(b.chargingTime === 0);
  r.push(b.dischargingTime === Infinity);
  document.getElementById('result').textContent = r.join(',');
});
");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact(Timeout = 600000)]
    public void RequestMediaKeySystemAccess_Rejects_Every_Key_System()
    {
        var result = Run(@"
navigator.requestMediaKeySystemAccess('com.widevine.alpha', [{ initDataTypes: ['cenc'] }])
  .then(function () { r.push('resolved'); })
  .catch(function (e) { r.push('rejected:' + e.name); })
  .then(function () {
    document.getElementById('result').textContent = r.join(',');
  });
");
        Assert.Contains("rejected:NotSupportedError", result);
    }
}
