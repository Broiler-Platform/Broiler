using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>navigator</c>'s object-valued surfaces: <c>storage</c>, <c>permissions</c> and
/// <c>userAgentData</c> — and the three that stay absent.
/// </summary>
/// <remarks>
/// <para>
/// Each of these is a whole API rather than a value, so each needed its own decision: whether a
/// present object answers a page's <c>'x' in navigator</c> detection <em>more</em> misleadingly than
/// absence does, which is the test that kept <c>speechSynthesis</c> and <c>navigator.bluetooth</c>
/// out. Three pass it because the question the interface exists to answer is one Broiler can answer
/// truthfully; three do not, and this file pins their absence so it stays a decision rather than
/// drifting into an omission.
/// </para>
/// <para>
/// Expectations are Chromium's measured answers except where the comment says otherwise —
/// <c>PermissionStatus.state</c> is the one deliberate divergence, and it is the point of the
/// feature rather than a shortfall.
/// </para>
/// </remarks>
public sealed class NavigatorSurfacesTests
{
    private static DomBridge Attach(out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!DOCTYPE html><html><body></body></html>", "https://example.com/index.html");
        return bridge;
    }

    private static string Eval(JSContext context, string body) =>
        context.Eval($$"""
            (() => {
                {{body}}
            })()
            """).ToString();

    /// <summary>
    /// Runs <paramref name="start"/>, then reads <paramref name="read"/> in a second evaluation.
    /// Every one of these surfaces answers with a promise, and a <c>then</c> callback runs at the
    /// microtask checkpoint that ends the evaluation which started it — so the result is readable in
    /// the next one and not in the same one.
    /// </summary>
    private static string EvalAfterMicrotasks(JSContext context, string start, string read)
    {
        context.Eval(start);
        return context.Eval(read).ToString();
    }

    /// <summary>
    /// The three interfaces are real globals whose instance is a singleton on <c>navigator</c>,
    /// carrying no own properties: the members are on the prototypes.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Surfaces_Are_Real_Interfaces_With_Members_On_Their_Prototypes()
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "StorageManager/Permissions/NavigatorUAData/0/0/0/" +
            "constructor,estimate,persist,persisted/constructor,query/" +
            "brands,constructor,getHighEntropyValues,mobile,platform,toJSON",
            Eval(context, """
                function own(o) { return Object.getOwnPropertyNames(o).length; }
                function proto(o) { return Object.getOwnPropertyNames(Object.getPrototypeOf(o)).sort().join(','); }
                return navigator.storage.constructor.name + '/' + navigator.permissions.constructor.name +
                       '/' + navigator.userAgentData.constructor.name + '/' +
                       own(navigator.storage) + '/' + own(navigator.permissions) + '/' + own(navigator.userAgentData) +
                       '/' + proto(navigator.storage) + '/' + proto(navigator.permissions) + '/' + proto(navigator.userAgentData);
                """));
    }

    /// <summary>None of them is constructible — they come from <c>navigator</c>, and a
    /// <c>PermissionStatus</c> from a query.</summary>
    [Fact(Timeout = 600000)]
    public void The_Interfaces_Are_Not_Constructible()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("TypeError/TypeError/TypeError/TypeError", Eval(context, """
            return ['StorageManager', 'Permissions', 'PermissionStatus', 'NavigatorUAData'].map(function (name) {
                try { new globalThis[name](); return 'ok'; } catch (e) { return e.name; }
            }).join('/');
            """));
    }

    /// <summary>
    /// <c>estimate()</c> reports zero used of zero available, and neither persistence call promises
    /// anything: Broiler implements none of the backends this interface counts.
    /// </summary>
    /// <remarks>
    /// It is the same pair the already-present <c>navigator.webkitTemporaryStorage</c> reports for
    /// the same question through the deprecated interface — the two would have disagreed by one of
    /// them being absent. <c>getDirectory()</c> is deliberately not here: the origin private file
    /// system's feature-detect is exactly <c>'getDirectory' in navigator.storage</c>.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void StorageManager_Reports_An_Empty_Quota_And_No_Persistence()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("usage,quota/0/0/false/false/false", EvalAfterMicrotasks(context, """
            var log = [];
            navigator.storage.estimate().then(function (e) {
                log[0] = Object.keys(e).join(',') + '/' + e.usage + '/' + e.quota;
            });
            navigator.storage.persisted().then(function (v) { log[1] = String(v); });
            navigator.storage.persist().then(function (v) { log[2] = String(v); });
            """, """
            log[0] + '/' + log[1] + '/' + log[2] + '/' + ('getDirectory' in navigator.storage)
            """));
    }

    /// <summary>
    /// Every permission query answers <c>"denied"</c>. Broiler grants no permission-gated capability
    /// and has no surface to prompt on.
    /// </summary>
    /// <remarks>
    /// This is the one deliberate divergence from Chromium, which answers <c>"prompt"</c>: that
    /// state promises a dialog the user will be shown, and there is none. It is also the state
    /// <c>Notification.permission</c> already reports, for the same reason — the two would have
    /// disagreed about the same capability.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void Every_Permission_Query_Answers_Denied()
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "notifications=denied,geolocation=denied,camera=denied,microphone=denied," +
            "persistent-storage=denied/PermissionStatus/notifications/denied/denied",
            EvalAfterMicrotasks(context, """
                var results = [], shape = '';
                ['notifications', 'geolocation', 'camera', 'microphone', 'persistent-storage'].forEach(function (name) {
                    navigator.permissions.query({ name: name }).then(function (status) {
                        results.push(name + '=' + status.state);
                    });
                });
                navigator.permissions.query({ name: 'notifications' }).then(function (status) {
                    shape = status.constructor.name + '/' + status.name + '/' + status.state;
                });
                """, """
                results.join(',') + '/' + shape + '/' + Notification.permission
                """));
    }

    /// <summary>
    /// A name outside the <c>PermissionName</c> enum rejects with a <c>TypeError</c> rather than
    /// answering a denial: the enum is validated before the permission is looked at, so a typo is
    /// reported as a typo.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Unknown_Permission_Name_Rejects_With_A_TypeError()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("object/TypeError/true", EvalAfterMicrotasks(context, """
            var kind = '', name = '', mentions = false;
            var promise = navigator.permissions.query({ name: 'nope' });
            kind = typeof promise;
            promise.then(function () { name = 'resolved'; }, function (e) {
                name = e.name;
                mentions = e.message.indexOf('not a valid enum value') >= 0;
            });
            """, """
            kind + '/' + name + '/' + mentions
            """));
    }

    /// <summary>
    /// <c>userAgentData</c> is derived from the one user-agent string, so the structured identity
    /// and the string cannot disagree — which is the whole argument for exposing it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void UserAgentData_Is_Derived_From_The_One_User_Agent_String()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("Broiler/1/false/Windows/true/true", Eval(context, """
            var data = navigator.userAgentData;
            var brand = data.brands[0];
            var json = data.toJSON();
            return brand.brand + '/' + brand.version + '/' + data.mobile + '/' + data.platform + '/' +
                   (navigator.userAgent.indexOf(brand.brand) >= 0) + '/' +
                   (json.platform === data.platform && json.mobile === data.mobile &&
                    json.brands[0].brand === brand.brand);
            """));
    }

    /// <summary>
    /// <c>getHighEntropyValues</c> answers the hints it is asked for and no others, always over the
    /// low-entropy trio.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void GetHighEntropyValues_Answers_Exactly_The_Hints_Requested()
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "brands,mobile,platform,architecture,bitness,platformVersion,uaFullVersion,fullVersionList" +
            "/x86/64/10.0.0/1.0/Broiler 1.0/brands,mobile,platform",
            EvalAfterMicrotasks(context, """
                var full = '', narrow = '';
                navigator.userAgentData.getHighEntropyValues(
                    ['architecture', 'bitness', 'platformVersion', 'uaFullVersion', 'fullVersionList']
                ).then(function (v) {
                    full = Object.keys(v).join(',') + '/' + v.architecture + '/' + v.bitness + '/' +
                           v.platformVersion + '/' + v.uaFullVersion + '/' +
                           v.fullVersionList[0].brand + ' ' + v.fullVersionList[0].version;
                });
                navigator.userAgentData.getHighEntropyValues([]).then(function (v) {
                    narrow = Object.keys(v).join(',');
                });
                """, """
                full + '/' + narrow
                """));
    }

    /// <summary>
    /// The three that stay absent, pinned so their absence remains a decision rather than drifting
    /// into an omission.
    /// </summary>
    /// <remarks>
    /// <c>connection</c> claims the user agent can report the connection's quality — Broiler
    /// measures none of it, and the interface has no "not known" state, so any value would be an
    /// invention rather than a negative answer. <c>mediaDevices</c> and <c>mediaCapabilities</c> are
    /// media surfaces whose capability decisions belong with the rest of media.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void The_Deliberately_Absent_Surfaces_Are_Absent()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("false,false,false", Eval(context, """
            return ['connection', 'mediaDevices', 'mediaCapabilities'].map(function (name) {
                return String(name in navigator);
            }).join(',');
            """));
    }
}
