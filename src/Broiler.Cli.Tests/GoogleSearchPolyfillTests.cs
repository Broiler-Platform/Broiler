using Broiler.HtmlBridge;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for the Google Search compliance polyfills (TODO-G1 through TODO-G21).
/// These validate that the JavaScript environment exposes all APIs needed for
/// Google Search scripts to execute without errors.
/// </summary>
public class GoogleSearchPolyfillTests
{
    /// <summary>
    /// Helper: execute JS in a DomBridge-attached context and return the result.
    /// </summary>
    private static string ExecJs(string jsCode)
    {
        var html = $@"<!doctype html>
<html><head><title>Test</title></head>
<body>
<div id=""result""></div>
<script>
{jsCode}
</script>
</body>
</html>";
        return CaptureService.ExecuteScriptsWithDom(html, "https://www.google.com");
    }

    // ---------------------------------------------------------------
    //  TODO-G1: DOM query null-vs-undefined
    // ---------------------------------------------------------------

    [Fact]
    public void GetElementById_Returns_Null_Not_Undefined_For_Missing_Element()
    {
        var result = ExecJs(@"
            var el = document.getElementById('nonexistent');
            document.getElementById('result').textContent = (el === null) ? 'NULL' : 'NOT_NULL:' + typeof el;
        ");
        Assert.Contains("NULL", result);
    }

    [Fact]
    public void QuerySelector_Returns_Null_Not_Undefined_For_Missing_Element()
    {
        var result = ExecJs(@"
            var el = document.querySelector('#nonexistent');
            document.getElementById('result').textContent = (el === null) ? 'NULL' : 'NOT_NULL:' + typeof el;
        ");
        Assert.Contains("NULL", result);
    }

    [Fact]
    public void QuerySelectorAll_Returns_Empty_Array_For_No_Matches()
    {
        var result = ExecJs(@"
            var els = document.querySelectorAll('.nonexistent');
            document.getElementById('result').textContent = 'LEN:' + els.length;
        ");
        Assert.Contains("LEN:0", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G2: performance.now()
    // ---------------------------------------------------------------

    [Fact]
    public void Performance_Now_Returns_Number()
    {
        var result = ExecJs(@"
            var t = performance.now();
            document.getElementById('result').textContent = 'TYPE:' + typeof t + ',GTE0:' + (t >= 0);
        ");
        Assert.Contains("TYPE:number", result);
        Assert.Contains("GTE0:true", result);
    }

    [Fact]
    public void Performance_TimeOrigin_Is_Number()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent = 'TYPE:' + typeof performance.timeOrigin + ',GT0:' + (performance.timeOrigin > 0);
        ");
        Assert.Contains("TYPE:number", result);
        Assert.Contains("GT0:true", result);
    }

    [Fact]
    public void Performance_GetEntriesByType_Returns_Array()
    {
        var result = ExecJs(@"
            var entries = performance.getEntriesByType('resource');
            document.getElementById('result').textContent = 'IS_ARRAY:' + Array.isArray(entries);
        ");
        Assert.Contains("IS_ARRAY:true", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G3: navigator.sendBeacon()
    // ---------------------------------------------------------------

    [Fact]
    public void Navigator_SendBeacon_Returns_True()
    {
        var result = ExecJs(@"
            var ok = navigator.sendBeacon('/log', 'data');
            document.getElementById('result').textContent = 'RESULT:' + ok;
        ");
        Assert.Contains("RESULT:true", result);
    }

    [Fact]
    public void Navigator_UserAgent_Is_String()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent = 'UA:' + typeof navigator.userAgent + ',HAS:' + (navigator.userAgent.length > 0);
        ");
        Assert.Contains("UA:string", result);
        Assert.Contains("HAS:true", result);
    }

    [Fact]
    public void Navigator_Language_Is_String()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent = 'LANG:' + navigator.language;
        ");
        Assert.Contains("LANG:en-US", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G4: clientWidth / clientHeight
    // ---------------------------------------------------------------

    [Fact]
    public void Html_Element_Has_ClientWidth()
    {
        var result = ExecJs(@"
            var html = document.documentElement || document.querySelector('html');
            document.getElementById('result').textContent = 'CW:' + html.clientWidth;
        ");
        Assert.Contains("CW:1024", result);
    }

    [Fact]
    public void Body_Element_Has_ClientHeight()
    {
        var result = ExecJs(@"
            var body = document.body || document.querySelector('body');
            document.getElementById('result').textContent = 'CH:' + body.clientHeight;
        ");
        Assert.Contains("CH:768", result);
    }

    [Fact]
    public void Window_InnerWidth_Returns_Viewport_Width()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent = 'W:' + window.innerWidth + ',H:' + window.innerHeight;
        ");
        Assert.Contains("W:1024", result);
        Assert.Contains("H:768", result);
    }

    [Fact]
    public void Element_GetBoundingClientRect_Returns_Object()
    {
        var result = ExecJs(@"
            var body = document.body || document.querySelector('body');
            var rect = body.getBoundingClientRect();
            document.getElementById('result').textContent = 'W:' + rect.width + ',H:' + rect.height + ',TOP:' + rect.top;
        ");
        Assert.Contains("TOP:0", result);
    }

    [Fact]
    public void Element_ClientDimensions_Ignore_Zoom()
    {
        var result = ExecJs(@"
            var plain = document.createElement('div');
            plain.style.width = '64px';
            plain.style.height = '64px';
            var zoomed = document.createElement('div');
            zoomed.style.width = '64px';
            zoomed.style.height = '64px';
            zoomed.style.zoom = '4';
            document.body.appendChild(plain);
            document.body.appendChild(zoomed);
            document.getElementById('result').textContent =
                'PW:' + plain.clientWidth + ',ZW:' + zoomed.clientWidth + ',ZH:' + zoomed.clientHeight;
        ");
        Assert.Contains("PW:64,ZW:64,ZH:64", result);
    }

    [Fact]
    public void Element_BoundingClientRect_Scales_With_Zoom()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            var zoomed = document.createElement('div');
            zoomed.style.width = '64px';
            zoomed.style.height = '64px';
            zoomed.style.zoom = '4';
            document.body.appendChild(zoomed);
            var rect = zoomed.getBoundingClientRect();
            document.getElementById('result').textContent = 'W:' + rect.width + ',H:' + rect.height;
        ");
        Assert.Contains("W:256,H:256", result);
    }

    [Fact]
    public void Element_ScrollTo_Updates_ScrollOffsets()
    {
        var result = ExecJs(@"
            var container = document.createElement('div');
            container.style.width = '100px';
            container.style.height = '100px';
            container.style.overflow = 'scroll';
            var content = document.createElement('div');
            content.style.width = '250px';
            content.style.height = '250px';
            container.appendChild(content);
            document.body.appendChild(container);
            container.scrollTo(13, 27);
            document.getElementById('result').textContent = 'L:' + container.scrollLeft + ',T:' + container.scrollTop;
        ");
        Assert.Contains("L:13,T:27", result);
    }

    [Fact]
    public void Element_Scroll_Alias_And_Object_Arguments_Update_Offsets()
    {
        var result = ExecJs(@"
            var container = document.createElement('div');
            container.style.width = '100px';
            container.style.height = '100px';
            container.style.overflow = 'scroll';
            var content = document.createElement('div');
            content.style.width = '250px';
            content.style.height = '250px';
            container.appendChild(content);
            document.body.appendChild(container);
            container.scroll(50, 60);
            container.scrollTo({ left: 75 });
            container.scrollBy({ top: 15 });
            container.scroll({});
            document.getElementById('result').textContent = 'L:' + container.scrollLeft + ',T:' + container.scrollTop;
        ");
        Assert.Contains("L:75,T:75", result);
    }

    [Fact]
    public void Element_ScrollOffsets_Clamp_And_Respect_WritingMode_Direction()
    {
        var result = ExecJs(@"
            function makeScroller(writingMode, direction) {
                var scroller = document.createElement('div');
                scroller.style.overflow = 'scroll';
                scroller.style.width = '150px';
                scroller.style.height = '100px';
                scroller.style.writingMode = writingMode;
                scroller.style.direction = direction;

                var content = document.createElement('div');
                content.style.width = '300px';
                content.style.height = '400px';
                scroller.appendChild(content);
                document.body.appendChild(scroller);

                scroller.scrollLeft = writingMode === 'vertical-lr' || direction !== 'rtl' ? 999 : -999;
                scroller.scrollTop = direction === 'rtl' && writingMode !== 'horizontal-tb' ? -999 : 999;
                return scroller.scrollLeft + ',' + scroller.scrollTop;
            }

            document.getElementById('result').textContent = [
                makeScroller('horizontal-tb', 'ltr'),
                makeScroller('horizontal-tb', 'rtl'),
                makeScroller('vertical-lr', 'ltr'),
                makeScroller('vertical-lr', 'rtl'),
                makeScroller('vertical-rl', 'ltr'),
                makeScroller('vertical-rl', 'rtl')
            ].join('|');
        ");
        Assert.Contains("150,300|-150,300|150,300|150,-300|0,300|-150,-300", result);
    }

    [Fact]
    public void Element_ClientMetrics_Ignore_Effective_Zoom()
    {
        var result = ExecJs(@"
            function makeBox(zoom) {
                var el = document.createElement('div');
                el.style.width = '64px';
                el.style.height = '64px';
                if (zoom) {
                    el.style.zoom = zoom;
                }
                return el;
            }

            var noZoom = makeBox();
            var zoomed = makeBox('4');
            var zoomedParent = makeBox('4');
            var directChild = makeBox();
            var wrapper = document.createElement('div');
            var indirectChild = makeBox();
            var bothZoomed = makeBox('4');

            wrapper.appendChild(indirectChild);
            zoomedParent.appendChild(directChild);
            zoomedParent.appendChild(wrapper);
            zoomedParent.appendChild(bothZoomed);

            document.body.appendChild(noZoom);
            document.body.appendChild(zoomed);
            document.body.appendChild(zoomedParent);

            var same =
                zoomed.clientTop === noZoom.clientTop &&
                zoomed.clientLeft === noZoom.clientLeft &&
                zoomed.clientWidth === noZoom.clientWidth &&
                zoomed.clientHeight === noZoom.clientHeight &&
                directChild.clientWidth === noZoom.clientWidth &&
                indirectChild.clientHeight === noZoom.clientHeight &&
                bothZoomed.clientWidth === noZoom.clientWidth;

            document.getElementById('result').textContent =
                'OK:' + same + ',W:' + noZoom.clientWidth + ',H:' + noZoom.clientHeight +
                ',T:' + noZoom.clientTop + ',L:' + noZoom.clientLeft;
        ");
        Assert.Contains("OK:true,W:64,H:64,T:0,L:0", result);
    }

    [Fact]
    public void Element_ClientAndScrollMetrics_Include_Padding_Without_Counting_Internal_Negative_Margins_As_Overflow()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function makeContainer(zoom) {
                var container = document.createElement('div');
                container.style.width = '20px';
                container.style.height = '20px';
                container.style.padding = '10px 20px';
                container.style.overflow = 'auto';
                if (zoom) {
                    container.style.zoom = zoom;
                }

                var child = document.createElement('div');
                child.style.width = '20px';
                child.style.height = '20px';
                child.style.margin = '-5px -7px';
                container.appendChild(child);
                document.body.appendChild(container);
                return [
                    container.clientWidth,
                    container.clientHeight,
                    container.scrollWidth,
                    container.scrollHeight
                ].join(',');
            }

            document.getElementById('result').textContent =
                makeContainer('1') + '|' + makeContainer('2');
        ");
        Assert.Contains("60,40,60,40|60,40,60,40", result);
    }

    [Fact]
    public void Element_ScrollMetrics_Include_Child_Zoom_Overflow_In_Raw_Css_Pixels()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function measure(containerZoom, childZoom) {
                var container = document.createElement('div');
                container.style.width = '20px';
                container.style.height = '20px';
                container.style.padding = '10px 20px';
                container.style.overflow = 'auto';
                if (containerZoom) {
                    container.style.zoom = containerZoom;
                }

                var child = document.createElement('div');
                child.style.width = '20px';
                child.style.height = '20px';
                if (childZoom) {
                    child.style.zoom = childZoom;
                }

                container.appendChild(child);
                document.body.appendChild(container);
                return [
                    container.clientWidth,
                    container.clientHeight,
                    container.scrollWidth,
                    container.scrollHeight
                ].join(',');
            }

            document.getElementById('result').textContent =
                measure('', '2') + '|' +
                measure('2', '') + '|' +
                measure('2', '2');
        ");
        Assert.Contains("60,40,80,60|60,40,60,40|60,40,80,60", result);
    }

    [Fact]
    public void Element_OffsetDimensions_Exclude_Target_Zoom_But_Include_Borders()
    {
        var result = ExecJs(@"
            function makeOuter(margin) {
                var el = document.createElement('div');
                el.style.width = '100px';
                el.style.height = '100px';
                el.style.border = '1px solid black';
                el.style.position = 'relative';
                el.style.margin = margin || '10px';
                return el;
            }

            function makeSquare(zoom) {
                var el = document.createElement('div');
                el.style.width = '10px';
                el.style.height = '10px';
                el.style.margin = '1px';
                el.style.position = 'relative';
                el.style.top = '10px';
                el.style.left = '10px';
                if (zoom) {
                    el.style.zoom = zoom;
                }
                return el;
            }

            var unzoomedOuter = makeOuter();
            var unzoomed = makeSquare();
            unzoomedOuter.appendChild(unzoomed);

            var zoomedOuter = makeOuter();
            zoomedOuter.style.zoom = '3';
            var zoomed = makeSquare('2');
            zoomedOuter.appendChild(zoomed);

            var outerDiv = makeOuter('30px');
            outerDiv.id = 'outer_div';
            var middle = document.createElement('div');
            middle.style.margin = '10px';
            middle.style.zoom = '2';
            var unzoomedInner = document.createElement('div');
            unzoomedInner.style.width = '10px';
            unzoomedInner.style.height = '10px';
            unzoomedInner.style.margin = '1px';
            middle.appendChild(unzoomedInner);
            outerDiv.appendChild(middle);

            var outerDiv2 = makeOuter('30px');
            var unzoomedMiddle = document.createElement('div');
            var zoomedInner = document.createElement('div');
            zoomedInner.style.zoom = '2';
            zoomedInner.style.width = '100px';
            zoomedInner.style.height = '100px';
            zoomedInner.style.margin = '1px';
            zoomedInner.style.border = '1px solid black';
            unzoomedMiddle.appendChild(zoomedInner);
            outerDiv2.appendChild(unzoomedMiddle);

            document.body.appendChild(unzoomedOuter);
            document.body.appendChild(zoomedOuter);
            document.body.appendChild(outerDiv);
            document.body.appendChild(outerDiv2);

            var ok =
                unzoomed.offsetWidth === zoomed.offsetWidth &&
                unzoomed.offsetHeight === zoomed.offsetHeight &&
                zoomedInner.offsetWidth === outerDiv.offsetWidth &&
                zoomedInner.offsetHeight === outerDiv.offsetHeight;

            document.getElementById('result').textContent =
                'OK:' + ok + ',UW:' + unzoomed.offsetWidth + ',ZW:' + zoomed.offsetWidth +
                ',IW:' + zoomedInner.offsetWidth + ',OW:' + outerDiv.offsetWidth;
        ");
        Assert.Contains("OK:true,UW:10,ZW:10,IW:102,OW:102", result);
    }

    [Fact]
    public void Element_OffsetPosition_Uses_OffsetParent_And_Excludes_Target_Zoom()
    {
        var result = ExecJs(@"
            function makeOuter(margin, zoom) {
                var el = document.createElement('div');
                el.style.width = '100px';
                el.style.height = '100px';
                el.style.border = '1px solid black';
                el.style.position = 'relative';
                el.style.margin = margin || '10px';
                if (zoom) {
                    el.style.zoom = zoom;
                }
                return el;
            }

            function makeSquare(className) {
                var el = document.createElement('div');
                el.style.width = '10px';
                el.style.height = '10px';
                el.style.margin = '1px';
                if (className === 'one') {
                    el.style.position = 'relative';
                    el.style.top = '10px';
                    el.style.left = '10px';
                } else if (className === 'two') {
                    el.style.position = 'absolute';
                    el.style.top = '20px';
                    el.style.left = '20px';
                    el.style.zoom = '2';
                } else if (className === 'three') {
                    el.style.position = 'absolute';
                    el.style.top = '10px';
                    el.style.left = '50px';
                    el.style.zoom = '0.5';
                }
                return el;
            }

            var unzoomedOuter = makeOuter();
            var unzoomedOne = makeSquare('one');
            var unzoomedTwo = makeSquare('two');
            var unzoomedThree = makeSquare('three');
            unzoomedOuter.appendChild(unzoomedOne);
            unzoomedOuter.appendChild(unzoomedTwo);
            unzoomedOuter.appendChild(unzoomedThree);

            var zoomedOuter = makeOuter('10px', '3');
            var zoomedOne = makeSquare('one');
            var zoomedTwo = makeSquare('two');
            var zoomedThree = makeSquare('three');
            zoomedOuter.appendChild(zoomedOne);
            zoomedOuter.appendChild(zoomedTwo);
            zoomedOuter.appendChild(zoomedThree);

            var outerDiv = makeOuter('30px');
            var zoomedMiddle = document.createElement('div');
            zoomedMiddle.style.margin = '10px';
            zoomedMiddle.style.zoom = '2';
            var unzoomedInner = document.createElement('div');
            unzoomedInner.style.width = '10px';
            unzoomedInner.style.height = '10px';
            unzoomedInner.style.margin = '1px';
            zoomedMiddle.appendChild(unzoomedInner);
            outerDiv.appendChild(zoomedMiddle);

            var outerDiv2 = makeOuter('30px');
            var unzoomedMiddle = document.createElement('div');
            var zoomedInner = document.createElement('div');
            zoomedInner.style.zoom = '2';
            zoomedInner.style.width = '100px';
            zoomedInner.style.height = '100px';
            zoomedInner.style.margin = '1px';
            zoomedInner.style.border = '1px solid black';
            unzoomedMiddle.appendChild(zoomedInner);
            outerDiv2.appendChild(unzoomedMiddle);

            document.body.appendChild(unzoomedOuter);
            document.body.appendChild(zoomedOuter);
            document.body.appendChild(outerDiv);
            document.body.appendChild(outerDiv2);

            document.getElementById('result').textContent = 'VALUES:' + [
                unzoomedOne.offsetTop, unzoomedOne.offsetLeft,
                unzoomedTwo.offsetTop, unzoomedTwo.offsetLeft,
                unzoomedThree.offsetTop, unzoomedThree.offsetLeft,
                zoomedOne.offsetTop, zoomedOne.offsetLeft,
                zoomedTwo.offsetTop, zoomedTwo.offsetLeft,
                zoomedThree.offsetTop, zoomedThree.offsetLeft,
                unzoomedInner.offsetTop, unzoomedInner.offsetLeft,
                zoomedInner.offsetTop, zoomedInner.offsetLeft
            ].join(',');
        ");
        Assert.Contains("VALUES:11,11,21,21,11,51,11,11,21,21,11,51,10,11,0,1", result);
    }

    [Fact]
    public void MatchMedia_Uses_Viewport_Dimensions_And_Viewport_Lengths()
    {
        var result = ExecJs(@"
            var checks = [
                window.matchMedia('(min-width: 1000px)').matches,
                window.matchMedia('(min-height: 700px)').matches,
                window.matchMedia('(min-width: 50vw)').matches,
                window.matchMedia('(max-height: calc(100vh))').matches,
                window.matchMedia('(min-width: 2000px)').matches
            ];

            document.getElementById('result').textContent = checks.join(',');
        ");
        Assert.Contains("true,true,true,true,false", result);
    }

    [Fact]
    public void MatchMedia_Uses_Vmin_And_Vmax_Viewport_Lengths()
    {
        var result = ExecJs(@"
            var checks = [
                window.matchMedia('(min-width: 50vmin)').matches,
                window.matchMedia('(min-width: 90vmax)').matches,
                window.matchMedia('(max-width: 200vmax)').matches
            ];

            document.getElementById('result').textContent = checks.join(',');
        ");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void MediaQueries_Use_MainDocument_Viewport_Lengths_In_Computed_Styles()
    {
        var result = ExecJs(@"
            var style = document.createElement('style');
            style.textContent = `
                #target { width: 20px; height: 20px; }
                @media (min-width: 50vw) and (max-height: calc(100vh)) {
                    #target { width: 40px; }
                }
            `;
            document.head.appendChild(style);

            var target = document.createElement('div');
            target.id = 'target';
            document.body.appendChild(target);

            document.getElementById('result').textContent =
                window.getComputedStyle(target).width;
        ");
        Assert.Contains("40px", result);
    }

    [Fact]
    public void MediaQueries_Use_Vmin_And_Vmax_Lengths_In_Computed_Styles()
    {
        var result = ExecJs(@"
            var style = document.createElement('style');
            style.textContent = `
                #target { width: 20px; height: 20px; }
                @media (min-width: 50vmin) and (max-width: 200vmax) {
                    #target { width: 40px; }
                }
            `;
            document.head.appendChild(style);

            var target = document.createElement('div');
            target.id = 'target';
            document.body.appendChild(target);

            document.getElementById('result').textContent =
                window.getComputedStyle(target).width;
        ");
        Assert.Contains("40px", result);
    }

    [Fact]
    public void ScrollIntoView_Applies_ScrollPadding_And_ScrollMargin()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function buildContainer(zoom) {
                var container = document.createElement('div');
                container.style.width = '200px';
                container.style.height = '100px';
                container.style.overflow = 'hidden';
                container.style.scrollPaddingTop = '20px';
                if (zoom) {
                    container.style.zoom = zoom;
                }

                var buffer = document.createElement('div');
                buffer.style.height = '1000px';
                var target = document.createElement('div');
                target.style.height = '20px';
                target.style.scrollMarginTop = '30px';
                var tail = document.createElement('div');
                tail.style.height = '1000px';

                container.appendChild(buffer);
                container.appendChild(target);
                container.appendChild(tail);
                document.body.appendChild(container);
                target.scrollIntoView();
                return container.scrollTop;
            }

            document.getElementById('result').textContent =
                buildContainer('1') + ',' + buildContainer('2');
        ");
        Assert.Contains("950,950", result);
    }

    [Fact]
    public void ScrollIntoView_Resolves_Inherited_ScrollPadding_And_ScrollMargin_Under_Zoom()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function buildContainer(zoom) {
                var host = document.createElement('div');
                host.style.scrollPaddingTop = '20px';

                var container = document.createElement('div');
                container.style.width = '200px';
                container.style.height = '100px';
                container.style.overflow = 'hidden';
                container.style.scrollPaddingTop = 'inherit';
                container.style.scrollMarginTop = '30px';
                if (zoom) {
                    container.style.zoom = zoom;
                }

                var buffer = document.createElement('div');
                buffer.style.height = '1000px';
                var target = document.createElement('div');
                target.style.height = '20px';
                target.style.scrollMarginTop = 'inherit';
                if (zoom) {
                    target.style.zoom = zoom;
                }
                var tail = document.createElement('div');
                tail.style.height = '1000px';

                container.appendChild(buffer);
                container.appendChild(target);
                container.appendChild(tail);
                host.appendChild(container);
                document.body.appendChild(host);
                target.scrollIntoView();
                return container.scrollTop;
            }

            document.getElementById('result').textContent =
                buildContainer('1') + ',' + buildContainer('2');
        ");
        Assert.Contains("950,950", result);
    }

    [Fact]
    public void ScrollIntoView_Scrolls_Absolutely_Positioned_Targets_In_Raw_Css_Pixels()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function buildContainer(zoom) {
                var container = document.createElement('div');
                container.style.position = 'relative';
                container.style.width = '150px';
                container.style.height = '150px';
                container.style.overflow = 'auto';
                if (zoom) {
                    container.style.zoom = zoom;
                }

                var content = document.createElement('div');
                content.style.width = '600px';
                content.style.height = '600px';

                var target = document.createElement('div');
                target.style.position = 'absolute';
                target.style.left = '300px';
                target.style.top = '240px';
                target.style.width = '20px';
                target.style.height = '20px';

                container.appendChild(content);
                container.appendChild(target);
                document.body.appendChild(container);
                target.scrollIntoView();
                return container.scrollLeft + ',' + container.scrollTop;
            }

            document.getElementById('result').textContent =
                buildContainer('1') + '|' + buildContainer('2');
        ");
        Assert.Contains("300,240|300,240", result);
    }

    [Fact]
    public void ScrollIntoView_Scrolls_Percentage_Positioned_Targets_In_Raw_Css_Pixels()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function buildContainer(position, zoom) {
                var container = document.createElement('div');
                container.style.position = position;
                container.style.width = '150px';
                container.style.height = '150px';
                container.style.overflow = 'auto';
                if (zoom) {
                    container.style.zoom = zoom;
                }

                var content = document.createElement('div');
                content.style.width = '600px';
                content.style.height = '600px';

                var target = document.createElement('div');
                target.style.position = 'absolute';
                target.style.left = '200%';
                target.style.top = '200%';
                target.style.width = '20px';
                target.style.height = '20px';

                container.appendChild(content);
                container.appendChild(target);
                document.body.appendChild(container);
                target.scrollIntoView();
                return container.scrollLeft + ',' + container.scrollTop;
            }

            document.getElementById('result').textContent =
                buildContainer('relative', '1') + '|' +
                buildContainer('relative', '2') + '|' +
                buildContainer('fixed', '1') + '|' +
                buildContainer('fixed', '2');
        ");
        Assert.Contains("300,300|300,300|300,300|300,300", result);
    }

    [Fact]
    public void FontRelative_Ch_Units_Resolve_To_Raw_Css_Pixels_Under_Zoom()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function measure(zoom) {
                var box = document.createElement('div');
                box.style.width = '5ch';
                box.style.height = '10ch';
                box.style.font = '16px monospace';
                if (zoom) {
                    box.style.zoom = zoom;
                }

                document.body.appendChild(box);
                return box.offsetWidth + ',' + box.offsetHeight;
            }

            document.getElementById('result').textContent =
                measure('1') + '|' + measure('2');
        ");
        Assert.Contains("40,80|40,80", result);
    }

    [Fact]
    public void FontRelative_Ex_Units_Resolve_To_Raw_Css_Pixels_Under_Zoom()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function measure(zoom) {
                var box = document.createElement('div');
                box.style.width = '5ex';
                box.style.height = '10ex';
                box.style.font = '16px monospace';
                if (zoom) {
                    box.style.zoom = zoom;
                }

                document.body.appendChild(box);
                return box.offsetWidth + ',' + box.offsetHeight;
            }

            document.getElementById('result').textContent =
                measure('1') + '|' + measure('2');
        ");
        Assert.Contains("40,80|40,80", result);
    }

    [Fact]
    public void FontRelative_Lh_Units_Resolve_From_Parent_LineHeight_Under_Zoom()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function measure(zoom) {
                var parent = document.createElement('div');
                parent.style.lineHeight = '20px';

                var box = document.createElement('div');
                box.style.font = '16px monospace';
                box.style.lineHeight = '2lh';
                box.style.width = '1lh';
                box.style.height = '1lh';
                if (zoom) {
                    box.style.zoom = zoom;
                }

                parent.appendChild(box);
                document.body.appendChild(parent);
                return box.offsetWidth + ',' + box.offsetHeight;
            }

            document.getElementById('result').textContent =
                measure('1') + '|' + measure('2');
        ");
        Assert.Contains("40,40|40,40", result);
    }

    [Fact]
    public void FontRelative_Rlh_Units_Resolve_From_Root_LineHeight_Under_Zoom()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function measure(zoom) {
                var box = document.createElement('div');
                box.style.width = '3rlh';
                box.style.height = '2rlh';
                if (zoom) {
                    box.style.zoom = zoom;
                }

                document.body.appendChild(box);
                return box.offsetWidth + ',' + box.offsetHeight;
            }

            document.getElementById('result').textContent =
                measure('1') + '|' + measure('2');
        ");
        Assert.Contains("57.599999999999994,38.4|57.599999999999994,38.4", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G6: Image() constructor
    // ---------------------------------------------------------------

    [Fact]
    public void Image_Constructor_Creates_Object()
    {
        var result = ExecJs(@"
            var img = new Image();
            img.src = 'test.png';
            document.getElementById('result').textContent = 'SRC:' + img.src + ',W:' + img.width;
        ");
        Assert.Contains("SRC:test.png", result);
        Assert.Contains("W:0", result);
    }

    [Fact]
    public void Image_Constructor_With_Dimensions()
    {
        var result = ExecJs(@"
            var img = new Image(100, 200);
            document.getElementById('result').textContent = 'W:' + img.width + ',H:' + img.height;
        ");
        Assert.Contains("W:100", result);
        Assert.Contains("H:200", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G7: document.cookie stub
    // ---------------------------------------------------------------

    [Fact]
    public void Document_Cookie_Read_Returns_String()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent = 'TYPE:' + typeof document.cookie;
        ");
        Assert.Contains("TYPE:string", result);
    }

    [Fact]
    public void Document_Cookie_Write_Does_Not_Throw()
    {
        var result = ExecJs(@"
            document.cookie = 'test=value; path=/';
            document.getElementById('result').textContent = 'OK:true';
        ");
        Assert.Contains("OK:true", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G10: IntersectionObserver
    // ---------------------------------------------------------------

    [Fact]
    public void IntersectionObserver_Constructor_Exists()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent = 'TYPE:' + typeof IntersectionObserver;
        ");
        Assert.Contains("TYPE:function", result);
    }

    [Fact]
    public void IntersectionObserver_Observe_Invokes_Callback()
    {
        var result = ExecJs(@"
            var called = false;
            var obs = new IntersectionObserver(function(entries) { called = true; });
            obs.observe(document.getElementById('result'));
            document.getElementById('result').textContent = 'CALLED:' + called;
        ");
        Assert.Contains("CALLED:true", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G11: ResizeObserver
    // ---------------------------------------------------------------

    [Fact]
    public void ResizeObserver_Constructor_Exists()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent = 'TYPE:' + typeof ResizeObserver;
        ");
        Assert.Contains("TYPE:function", result);
    }

    [Fact]
    public void ResizeObserver_Observe_Does_Not_Throw()
    {
        var result = ExecJs(@"
            var obs = new ResizeObserver(function() {});
            obs.observe(document.getElementById('result'));
            obs.disconnect();
            document.getElementById('result').textContent = 'OK:true';
        ");
        Assert.Contains("OK:true", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G13: TextEncoder / TextDecoder
    // ---------------------------------------------------------------

    [Fact]
    public void TextEncoder_Encode_Returns_Uint8Array()
    {
        var result = ExecJs(@"
            var encoder = new TextEncoder();
            var bytes = encoder.encode('Hello');
            document.getElementById('result').textContent = 'LEN:' + bytes.length + ',B0:' + bytes[0];
        ");
        Assert.Contains("LEN:5", result);
        Assert.Contains("B0:72", result); // 'H' = 72
    }

    [Fact]
    public void TextDecoder_Decode_Returns_String()
    {
        var result = ExecJs(@"
            var encoder = new TextEncoder();
            var decoder = new TextDecoder();
            var bytes = encoder.encode('Test');
            var str = decoder.decode(bytes);
            document.getElementById('result').textContent = 'STR:' + str;
        ");
        Assert.Contains("STR:Test", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G14: URL / URLSearchParams
    // ---------------------------------------------------------------

    [Fact]
    public void URLSearchParams_Get_Returns_Value()
    {
        var result = ExecJs(@"
            var params = new URLSearchParams('?q=hello&lang=en');
            document.getElementById('result').textContent = 'Q:' + params.get('q') + ',LANG:' + params.get('lang');
        ");
        Assert.Contains("Q:hello", result);
        Assert.Contains("LANG:en", result);
    }

    [Fact]
    public void URLSearchParams_Has_Returns_Boolean()
    {
        var result = ExecJs(@"
            var params = new URLSearchParams('q=test');
            document.getElementById('result').textContent = 'HAS_Q:' + params.has('q') + ',HAS_X:' + params.has('x');
        ");
        Assert.Contains("HAS_Q:true", result);
        Assert.Contains("HAS_X:false", result);
    }

    [Fact]
    public void URL_Constructor_Parses_Href()
    {
        var result = ExecJs(@"
            var u = new URL('https://www.google.com/search?q=test#top');
            document.getElementById('result').textContent =
                'HOST:' + u.hostname + ',PATH:' + u.pathname + ',SEARCH:' + u.search + ',HASH:' + u.hash;
        ");
        Assert.Contains("HOST:www.google.com", result);
        Assert.Contains("PATH:/search", result);
        Assert.Contains("SEARCH:?q=test", result);
        Assert.Contains("HASH:#top", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G16: AbortController
    // ---------------------------------------------------------------

    [Fact]
    public void AbortController_Constructor_Exists()
    {
        var result = ExecJs(@"
            var ac = new AbortController();
            document.getElementById('result').textContent = 'ABORTED:' + ac.signal.aborted;
        ");
        Assert.Contains("ABORTED:false", result);
    }

    [Fact]
    public void AbortController_Abort_Sets_Signal()
    {
        var result = ExecJs(@"
            var ac = new AbortController();
            ac.abort();
            document.getElementById('result').textContent = 'ABORTED:' + ac.signal.aborted;
        ");
        Assert.Contains("ABORTED:true", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G17: CustomEvent (verify existing)
    // ---------------------------------------------------------------

    [Fact]
    public void CustomEvent_Constructor_Exists()
    {
        var result = ExecJs(@"
            var ev = new CustomEvent('test', { detail: 42 });
            document.getElementById('result').textContent = 'TYPE:' + ev.type + ',DETAIL:' + ev.detail;
        ");
        Assert.Contains("TYPE:test", result);
        Assert.Contains("DETAIL:42", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G18: crypto.getRandomValues()
    // ---------------------------------------------------------------

    [Fact]
    public void Crypto_GetRandomValues_Fills_Array()
    {
        var result = ExecJs(@"
            var arr = new Uint8Array(4);
            crypto.getRandomValues(arr);
            document.getElementById('result').textContent = 'LEN:' + arr.length;
        ");
        Assert.Contains("LEN:4", result);
    }

    [Fact]
    public void Crypto_RandomUUID_Returns_String()
    {
        var result = ExecJs(@"
            var uuid = crypto.randomUUID();
            document.getElementById('result').textContent = 'LEN:' + uuid.length + ',DASHES:' + (uuid.indexOf('-') > 0);
        ");
        Assert.Contains("LEN:36", result);
        Assert.Contains("DASHES:true", result);
    }

    // ---------------------------------------------------------------
    //  Window.screen properties
    // ---------------------------------------------------------------

    [Fact]
    public void Window_Screen_Has_Dimensions()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent = 'W:' + screen.width + ',H:' + screen.height;
        ");
        Assert.Contains("W:1024", result);
        Assert.Contains("H:768", result);
    }

    // ---------------------------------------------------------------
    //  Combined: Google-style script should not crash
    // ---------------------------------------------------------------

    [Fact]
    public void GoogleStyle_Init_Script_Runs_Without_Crash()
    {
        var result = ExecJs(@"
            var results = [];
            try { performance.now(); results.push('PERF_OK'); } catch(e) { results.push('PERF_NO'); }
            try { navigator.sendBeacon('/log', 'data'); results.push('BEACON_OK'); } catch(e) { results.push('BEACON_NO'); }
            try { var html = document.documentElement || document.querySelector('html'); results.push('DIM_OK:' + html.clientWidth); } catch(e) { results.push('DIM_NO'); }
            try { new URL('https://www.google.com/search?q=test'); results.push('URL_OK'); } catch(e) { results.push('URL_NO'); }
            try { new AbortController(); results.push('ABORT_OK'); } catch(e) { results.push('ABORT_NO'); }
            try { var obs = new IntersectionObserver(function() {}); obs.disconnect(); results.push('IO_OK'); } catch(e) { results.push('IO_NO'); }
            try { new TextEncoder().encode('test'); results.push('TE_OK'); } catch(e) { results.push('TE_NO'); }
            try { var img = new Image(); img.src = 'pixel.gif'; results.push('IMG_OK'); } catch(e) { results.push('IMG_NO'); }
            document.getElementById('result').textContent = results.join(',');
        ");
        Assert.Contains("PERF_OK", result);
        Assert.Contains("BEACON_OK", result);
        Assert.Contains("DIM_OK:1024", result);
        Assert.Contains("URL_OK", result);
        Assert.Contains("ABORT_OK", result);
        Assert.Contains("IO_OK", result);
        Assert.Contains("TE_OK", result);
        Assert.Contains("IMG_OK", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G9: -webkit- CSS prefix mapping (via getComputedStyle)
    // ---------------------------------------------------------------

    [Fact]
    public void Webkit_Prefixed_Property_Mapped_To_Unprefixed()
    {
        var result = ExecJs(@"
            var div = document.createElement('div');
            div.style.cssText = '-webkit-transform: rotate(45deg)';
            document.body.appendChild(div);
            var cs = window.getComputedStyle(div);
            document.getElementById('result').textContent =
                'WEBKIT:' + (cs['-webkit-transform'] || cs.webkitTransform || 'NONE') +
                ',STD:' + (cs['transform'] || cs.transform || 'NONE');
        ");
        Assert.Contains("rotate(45deg)", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G12: MutationObserver with _records tracking
    // ---------------------------------------------------------------

    [Fact]
    public void MutationObserver_Has_Records_Array()
    {
        var result = ExecJs(@"
            var obs = new MutationObserver(function() {});
            document.getElementById('result').textContent =
                'HAS_RECORDS:' + Array.isArray(obs._records) +
                ',LEN:' + obs._records.length;
        ");
        Assert.Contains("HAS_RECORDS:true", result);
        Assert.Contains("LEN:0", result);
    }

    [Fact]
    public void MutationObserver_TakeRecords_Returns_And_Clears()
    {
        var result = ExecJs(@"
            var obs = new MutationObserver(function() {});
            obs._records.push({ type: 'childList' });
            var taken = obs.takeRecords();
            document.getElementById('result').textContent =
                'TAKEN:' + taken.length +
                ',AFTER:' + obs._records.length;
        ");
        Assert.Contains("TAKEN:1", result);
        Assert.Contains("AFTER:0", result);
    }

    [Fact]
    public void MutationObserver_Disconnect_Clears_Records()
    {
        var result = ExecJs(@"
            var obs = new MutationObserver(function() {});
            obs._records.push({ type: 'attributes' });
            obs.disconnect();
            document.getElementById('result').textContent =
                'RECORDS:' + obs._records.length +
                ',TARGETS:' + obs._targets.length;
        ");
        Assert.Contains("RECORDS:0", result);
        Assert.Contains("TARGETS:0", result);
    }

    [Fact]
    public void MutationObserver_Notify_Invokes_Callback()
    {
        var result = ExecJs(@"
            var received = null;
            var obs = new MutationObserver(function(records) { received = records; });
            obs._notify([{ type: 'childList', target: document.body }]);
            document.getElementById('result').textContent =
                'CALLED:' + (received !== null) +
                ',LEN:' + (received ? received.length : -1) +
                ',TYPE:' + (received ? received[0].type : 'none');
        ");
        Assert.Contains("CALLED:true", result);
        Assert.Contains("LEN:1", result);
        Assert.Contains("TYPE:childList", result);
    }

    // ---------------------------------------------------------------
    //  TODO-G20: Async script detection
    // ---------------------------------------------------------------

    [Fact]
    public void ScriptExtractor_Separates_Async_Scripts()
    {
        var extractor = new ScriptExtractor();
        var html = @"<html><head>
            <script>var a = 1;</script>
            <script async>var b = 2;</script>
            <script defer>var c = 3;</script>
            <script async src=""data:text/javascript,var d = 4;""></script>
        </head><body></body></html>";

        var result = extractor.ExtractAll(html);
        Assert.Single(result.Scripts);           // 'var a = 1;'
        Assert.Single(result.DeferredScripts);   // 'var c = 3;'
        Assert.Equal(2, result.AsyncScripts.Count); // 'var b = 2;' + 'var d = 4;'
    }
}
