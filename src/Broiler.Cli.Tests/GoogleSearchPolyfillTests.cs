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
    public void Document_ElementFromPoint_Uses_Hit_Test_Order_And_Skips_PointerEvents_None()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            var purple = document.createElement('div');
            purple.id = 'purple';
            purple.style.width = '60px';
            purple.style.height = '60px';
            document.body.appendChild(purple);

            var yellow = document.createElement('div');
            yellow.id = 'yellow';
            yellow.style.width = '60px';
            yellow.style.height = '60px';
            document.body.appendChild(yellow);

            var overlay = document.createElement('div');
            overlay.id = 'overlay';
            overlay.style.position = 'absolute';
            overlay.style.left = '0';
            overlay.style.top = '60px';
            overlay.style.width = '60px';
            overlay.style.height = '60px';
            overlay.style.pointerEvents = 'none';
            document.body.appendChild(overlay);

            document.getElementById('result').textContent = [
                document.elementFromPoint(10, 10).id,
                document.elementFromPoint(10, 70).id,
                document.elementFromPoint(-1, -1) === null
            ].join('|');
        ");

        Assert.Contains("purple|yellow|true", result);
    }

    [Fact]
    public void Document_ElementsFromPoint_Returns_Target_Then_Ancestors_And_Viewport_Bounds()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            var target = document.createElement('div');
            target.id = 'target';
            target.style.width = '40px';
            target.style.height = '40px';
            document.body.appendChild(target);

            document.getElementById('result').textContent = [
                document.elementsFromPoint(10, 10)[0].id,
                document.elementsFromPoint(-1, -1).length,
                document.elementsFromPoint(1100, 10).length
            ].join('|');
        ");

        Assert.Contains("target|0|0", result);
    }

    [Fact]
    public void Document_HitTesting_Uses_Html_But_Not_Body_For_Iframe_Viewport_Fallback()
    {
        var result = ExecJs(@"
            var iframe = document.createElement('iframe');
            iframe.width = '';
            iframe.height = '';
            iframe.srcdoc = '<!DOCTYPE html><html><body><div style=""height:20px""></div></body></html>';
            document.body.appendChild(iframe);

            var doc = iframe.contentDocument;
            var hits = doc.elementsFromPoint(0, 100);
            document.getElementById('result').textContent = [
                hits.length,
                hits[0] && (hits[0].id || hits[0].tagName),
                hits[1] || null
            ].join('|');
        ");

        Assert.Contains("1|HTML|", result);
    }

    [Fact]
    public void Document_HitTesting_Tracks_AutoSized_Ancestors_With_Negative_Margins()
    {
        var result = ExecJs(@"
            document.body.innerHTML = '<div id=""outer"" style=""background:yellow""><div id=""inner"" style=""width:100px;height:100px;margin-bottom:-100px;background:lime;""></div>Hello</div>';
            var outer = document.getElementById('outer');
            var rect = outer.getBoundingClientRect();
            var hits = document.elementsFromPoint(rect.left + 1, rect.top + 1);
            document.getElementById('result').textContent = [
                document.elementFromPoint(rect.left + 1, rect.top + 1).id,
                Array.prototype.map.call(hits, function (node) { return node.id || node.tagName; }).join('>')
            ].join('|');
        ");

        Assert.Contains("outer|inner>outer>BODY>HTML", result);
    }

    [Fact]
    public void AutoSized_ScrollMetrics_Ignore_MarginOnly_NonOverflow_Cases()
    {
        var result = ExecJs(@"
            document.body.innerHTML = '<style>#target div{height:20px;min-width:20px;background:green;margin:20px 10px;}</style><div id=""target""><div><div></div></div><div></div><div></div><div></div></div>';
            var target = document.getElementById('target');
            var cases = [
                ['visible', 'block', '0', '0'],
                ['hidden', 'flow-root', '2px', '3px solid']
            ];
            document.getElementById('result').textContent = cases.map(function (entry) {
                target.style.overflow = entry[0];
                target.style.display = entry[1];
                target.style.padding = entry[2];
                target.style.border = entry[3];
                return String(target.scrollHeight === target.clientHeight && target.scrollWidth === target.clientWidth);
            }).join('|');
        ");

        Assert.Contains("true|true", result);
    }

    [Fact]
    public void Document_HitTesting_Returns_Null_For_Documents_Without_A_Viewport()
    {
        var result = ExecJs(@"
            var doc = document.implementation.createHTMLDocument('foo');
            document.getElementById('result').textContent = [
                doc.elementFromPoint(0, 0) === null,
                doc.elementsFromPoint(0, 0).length
            ].join('|');
        ");

        Assert.Contains("true|0", result);
    }

    [Fact]
    public void Document_HitTesting_Uses_Svg_Viewports_And_Rect_Geometry()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            var svgNs = 'http://www.w3.org/2000/svg';
            var svg = document.createElementNS(svgNs, 'svg');
            svg.id = 'svgRoot';
            svg.setAttribute('width', '180');
            svg.setAttribute('height', '140');
            var rect = document.createElementNS(svgNs, 'rect');
            rect.id = 'svgRect';
            rect.setAttribute('x', '50');
            rect.setAttribute('y', '50');
            rect.setAttribute('width', '60');
            rect.setAttribute('height', '60');
            rect.setAttribute('fill', '#0086B2');
            svg.appendChild(rect);
            document.body.insertBefore(svg, document.getElementById('result'));

            var svg = document.getElementById('svgRoot');
            var svgRect = svg.getBoundingClientRect();
            var rootHit = document.elementFromPoint(Math.round(svgRect.left + svgRect.width / 2), 10);
            var rectHit = document.elementFromPoint(90, 70);
            var rectHits = document.elementsFromPoint(90, 70);

            document.getElementById('result').textContent = [
                svgRect.width,
                svgRect.height,
                rootHit && (rootHit.id || rootHit.tagName),
                rectHit && (rectHit.id || rectHit.tagName),
                rectHits[0] && (rectHits[0].id || rectHits[0].tagName),
                rectHits[1] && (rectHits[1].id || rectHits[1].tagName)
            ].join('|');
        ");

        Assert.Contains("180|140|svgRoot|svgRect|svgRect|svgRoot", result);
    }

    [Fact]
    public void Document_HitTesting_Keeps_Inline_Svg_Roots_In_Normal_Flow()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            var svgNs = 'http://www.w3.org/2000/svg';

            var first = document.createElementNS(svgNs, 'svg');
            first.id = 'firstSvg';
            first.setAttribute('width', '180');
            first.setAttribute('height', '98');
            document.body.insertBefore(first, document.getElementById('result'));

            var second = document.createElementNS(svgNs, 'svg');
            second.id = 'secondSvg';
            second.setAttribute('width', '180');
            second.setAttribute('height', '140');
            var rect = document.createElementNS(svgNs, 'rect');
            rect.id = 'secondRect';
            rect.setAttribute('x', '50');
            rect.setAttribute('y', '50');
            rect.setAttribute('width', '60');
            rect.setAttribute('height', '60');
            second.appendChild(rect);
            document.body.insertBefore(second, document.getElementById('result'));

            var firstRect = first.getBoundingClientRect();
            var secondRect = second.getBoundingClientRect();
            var hit = document.elementFromPoint(80, 160);
            var hits = document.elementsFromPoint(80, 160);

            document.getElementById('result').textContent = [
                firstRect.top,
                secondRect.top,
                hit && (hit.id || hit.tagName),
                hits[0] && (hits[0].id || hits[0].tagName),
                hits[1] && (hits[1].id || hits[1].tagName)
            ].join('|');
        ");

        Assert.Contains("0|98|secondRect|secondRect|secondSvg", result);
    }

    [Fact]
    public void Document_HitTesting_Uses_Svg_Groups_Images_ForeignObject_And_Translate()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            var svgNs = 'http://www.w3.org/2000/svg';
            var svg = document.createElementNS(svgNs, 'svg');
            svg.id = 'svgRoot';
            svg.setAttribute('width', '300');
            svg.setAttribute('height', '300');

            var middleG1 = document.createElementNS(svgNs, 'g');
            middleG1.id = 'middleG1';
            var middleG2 = document.createElementNS(svgNs, 'g');
            middleG2.id = 'middleG2';
            var middleRect1 = document.createElementNS(svgNs, 'rect');
            middleRect1.id = 'middleRect1';
            middleRect1.setAttribute('x', '105');
            middleRect1.setAttribute('y', '105');
            middleRect1.setAttribute('width', '90');
            middleRect1.setAttribute('height', '90');
            var middleRect2 = document.createElementNS(svgNs, 'rect');
            middleRect2.id = 'middleRect2';
            middleRect2.setAttribute('x', '110');
            middleRect2.setAttribute('y', '110');
            middleRect2.setAttribute('width', '80');
            middleRect2.setAttribute('height', '80');
            middleG2.appendChild(middleRect1);
            middleG2.appendChild(middleRect2);
            middleG1.appendChild(middleG2);
            svg.appendChild(middleG1);

            var imageGroup = document.createElementNS(svgNs, 'g');
            imageGroup.id = 'imageGroup';
            var image1 = document.createElementNS(svgNs, 'image');
            image1.id = 'image1';
            image1.setAttribute('x', '5');
            image1.setAttribute('y', '205');
            image1.setAttribute('width', '90');
            image1.setAttribute('height', '90');
            image1.setAttributeNS('http://www.w3.org/1999/xlink', 'href', 'data:image/gif;base64,R0lGODlhAQABAAAAACw=');
            var image2 = document.createElementNS(svgNs, 'image');
            image2.id = 'image2';
            image2.setAttribute('x', '10');
            image2.setAttribute('y', '210');
            image2.setAttribute('width', '80');
            image2.setAttribute('height', '80');
            image2.setAttributeNS('http://www.w3.org/1999/xlink', 'href', 'data:image/gif;base64,R0lGODlhAQABAAAAACw=');
            imageGroup.appendChild(image1);
            imageGroup.appendChild(image2);
            svg.appendChild(imageGroup);

            var fo = document.createElementNS(svgNs, 'foreignObject');
            fo.id = 'fo';
            fo.setAttribute('x', '210');
            fo.setAttribute('y', '110');
            fo.setAttribute('width', '80');
            fo.setAttribute('height', '80');
            var foDiv = document.createElement('div');
            foDiv.id = 'foDiv';
            foDiv.style.width = '80px';
            foDiv.style.height = '80px';
            fo.appendChild(foDiv);
            svg.appendChild(fo);

            var translatedOuter = document.createElementNS(svgNs, 'g');
            translatedOuter.id = 'translatedOuter';
            translatedOuter.setAttribute('transform', 'translate(200, 200)');
            var translatedInner = document.createElementNS(svgNs, 'g');
            translatedInner.id = 'translatedInner';
            translatedInner.setAttribute('transform', 'translate(5, 5)');
            var translatedRect1 = document.createElementNS(svgNs, 'rect');
            translatedRect1.id = 'translatedRect1';
            translatedRect1.setAttribute('x', '0');
            translatedRect1.setAttribute('y', '0');
            translatedRect1.setAttribute('width', '90');
            translatedRect1.setAttribute('height', '90');
            var translatedRect2 = document.createElementNS(svgNs, 'rect');
            translatedRect2.id = 'translatedRect2';
            translatedRect2.setAttribute('x', '5');
            translatedRect2.setAttribute('y', '5');
            translatedRect2.setAttribute('width', '80');
            translatedRect2.setAttribute('height', '80');
            translatedInner.appendChild(translatedRect1);
            translatedInner.appendChild(translatedRect2);
            translatedOuter.appendChild(translatedInner);
            svg.appendChild(translatedOuter);

            document.body.insertBefore(svg, document.getElementById('result'));

            var middleHits = document.elementsFromPoint(125, 125);
            var imageHits = document.elementsFromPoint(50, 250);
            var foreignObjectHits = document.elementsFromPoint(250, 150);
            var translatedHits = document.elementsFromPoint(250, 250);

            document.getElementById('result').textContent = [
                middleHits.slice(0, 5).map((node) => node.id || node.tagName).join(','),
                imageHits.slice(0, 4).map((node) => node.id || node.tagName).join(','),
                foreignObjectHits.slice(0, 3).map((node) => node.id || node.tagName).join(','),
                translatedHits.slice(0, 5).map((node) => node.id || node.tagName).join(',')
            ].join('|');
        ");

        Assert.Contains(
            "middleRect2,middleRect1,middleG2,middleG1,svgRoot|image2,image1,imageGroup,svgRoot|foDiv,fo,svgRoot|translatedRect2,translatedRect1,translatedInner,translatedOuter,svgRoot",
            result);
    }

    [Fact]
    public void Document_HitTesting_Uses_Svg_Text_Tspan_And_TextPath_Content()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            var svgNs = 'http://www.w3.org/2000/svg';
            var xlinkNs = 'http://www.w3.org/1999/xlink';
            var svg = document.createElementNS(svgNs, 'svg');
            svg.id = 'svgRoot';
            svg.setAttribute('width', '300');
            svg.setAttribute('height', '300');
            svg.style.margin = '100px';
            svg.style.display = 'block';

            var defs = document.createElementNS(svgNs, 'defs');
            var path = document.createElementNS(svgNs, 'path');
            path.id = 'path';
            path.setAttribute('d', 'M10,170h1000');
            defs.appendChild(path);
            svg.appendChild(defs);

            var text1 = document.createElementNS(svgNs, 'text');
            text1.id = 'text1';
            text1.setAttribute('x', '10');
            text1.setAttribute('y', '50');
            text1.setAttribute('font-size', '50');
            text1.textContent = 'Some text';
            svg.appendChild(text1);

            var text2 = document.createElementNS(svgNs, 'text');
            text2.id = 'text2';
            text2.setAttribute('x', '10');
            text2.setAttribute('y', '110');
            text2.setAttribute('font-size', '50');
            var tspan1 = document.createElementNS(svgNs, 'tspan');
            tspan1.id = 'tspan1';
            tspan1.textContent = 'Some text';
            text2.appendChild(tspan1);
            svg.appendChild(text2);

            var text3 = document.createElementNS(svgNs, 'text');
            text3.id = 'text3';
            text3.setAttribute('font-size', '50');
            var textPath = document.createElementNS(svgNs, 'textPath');
            textPath.id = 'textpath1';
            textPath.setAttributeNS(xlinkNs, 'xlink:href', '#path');
            textPath.textContent = 'Some text';
            text3.appendChild(textPath);
            svg.appendChild(text3);

            var text4 = document.createElementNS(svgNs, 'text');
            text4.id = 'text4';
            text4.setAttribute('x', '10');
            text4.setAttribute('y', '230');
            text4.setAttribute('font-size', '50');
            text4.appendChild(document.createTextNode('Text under'));
            var tspan2 = document.createElementNS(svgNs, 'tspan');
            tspan2.id = 'tspan2';
            tspan2.setAttribute('x', '10');
            tspan2.textContent = 'Text over';
            text4.appendChild(tspan2);
            svg.appendChild(text4);

            document.body.insertBefore(svg, document.getElementById('result'));

            var firstHits = document.elementsFromPoint(125, 125);
            var secondHits = document.elementsFromPoint(125, 185);
            var thirdHits = document.elementsFromPoint(125, 245);
            var fourthHits = document.elementsFromPoint(125, 305);

            document.getElementById('result').textContent = [
                firstHits[0] && (firstHits[0].id || firstHits[0].tagName),
                firstHits[1] && (firstHits[1].id || firstHits[1].tagName),
                secondHits[0] && (secondHits[0].id || secondHits[0].tagName),
                thirdHits[0] && (thirdHits[0].id || thirdHits[0].tagName),
                fourthHits[0] && (fourthHits[0].id || fourthHits[0].tagName),
                fourthHits[1] && (fourthHits[1].id || fourthHits[1].tagName)
            ].join('|');
        ");

        Assert.Contains("text1|svgRoot|tspan1|textpath1|tspan2|text4", result);
    }

    [Fact]
    public void Document_HitTesting_Uses_Table_Cell_Layout_For_Rtl_And_Vertical_Writing_Modes()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            var sandbox = document.createElement('div');
            sandbox.id = 'sandbox';
            var table = document.createElement('table');
            table.id = 'testtable';
            table.style.margin = '100px';
            table.style.width = '200px';
            table.style.height = '200px';

            for (var rowIndex = 1; rowIndex <= 4; rowIndex++) {
                var row = document.createElement('tr');
                row.id = 'tr' + rowIndex;
                for (var cellIndex = 1; cellIndex <= 4; cellIndex++) {
                    var cell = document.createElement('td');
                    cell.id = 'td' + rowIndex + cellIndex;
                    row.appendChild(cell);
                }
                table.appendChild(row);
            }

            sandbox.appendChild(table);
            document.body.insertBefore(sandbox, document.getElementById('result'));

            function summarize(x, y, count) {
                return document.elementsFromPoint(x, y).slice(0, count).map((node) => node.id || node.tagName).join(',');
            }

            var initialCell = summarize(125, 125, 5);
            var initialGap = summarize(199, 199, 4);
            table.className = 'rtl';
            table.style.direction = 'rtl';
            var rtlCell = summarize(125, 125, 1);
            table.className = 'tblr';
            table.style.writingMode = 'vertical-lr';
            table.style.direction = 'ltr';
            var verticalBottomLeft = summarize(125, 275, 1);
            var verticalTopRight = summarize(275, 125, 1);

            document.getElementById('result').textContent = [
                initialCell,
                initialGap,
                rtlCell,
                verticalBottomLeft,
                verticalTopRight
            ].join('|');
        ");

        Assert.Contains("td11,testtable,sandbox,BODY,HTML|testtable,sandbox,BODY,HTML|td14|td14|td41", result);
    }

    [Fact]
    public void Document_HitTesting_Uses_Image_Map_Areas_Before_Associated_Images()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            var image = document.createElement('img');
            image.id = 'dinos';
            image.setAttribute('usemap', '#dinos_map');
            image.setAttribute('width', '364');
            image.setAttribute('height', '40');
            image.style.display = 'block';
            image.src = 'data:image/gif;base64,R0lGODlhAQABAAAAACw=';

            var map = document.createElement('map');
            map.id = 'dinos_map';
            map.name = 'dinos_map';
            var area = document.createElement('area');
            area.id = 'rectG';
            area.shape = 'rect';
            area.coords = '0,0,90,100';
            area.href = '#';
            map.appendChild(area);

            document.body.insertBefore(image, document.getElementById('result'));
            document.body.insertBefore(map, document.getElementById('result'));

            var rect = image.getBoundingClientRect();
            document.getElementById('result').textContent = [
                document.elementFromPoint(rect.left + 45, rect.top + 20).id,
                document.elementsFromPoint(rect.left + 45, rect.top + 20).slice(0, 4).map((node) => node.id || node.tagName).join(','),
                document.elementFromPoint(rect.left + 92, rect.top + 2).id
            ].join('|');
        ");

        Assert.Contains("rectG|rectG,dinos,BODY,HTML|dinos", result);
    }

    [Fact]
    public void Document_HitTesting_Excludes_Rounded_Corners_From_Fieldset_Hits()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            document.body.innerHTML = '<div style=""position:absolute;width:200px;height:200px;right:0;top:0""><div id=""fieldsetDiv"" style=""position:absolute;top:0;left:0;width:60px;height:60px;background:rebeccapurple""></div><fieldset id=""fieldset"" style=""position:absolute;top:100px;left:100px;width:60px;height:60px;border-radius:100px""><span style=""position:absolute;top:-100px;left:-100px;width:1px;height:1px""></span></fieldset></div><pre id=""result""></pre>';
            var fieldsetDivRect = document.getElementById('fieldsetDiv').getBoundingClientRect();
            var fieldsetRect = document.getElementById('fieldset').getBoundingClientRect();
            document.getElementById('result').textContent = [
                document.elementFromPoint(fieldsetDivRect.left + fieldsetDivRect.width / 2, fieldsetDivRect.top + fieldsetDivRect.height / 2).id,
                document.elementFromPoint(fieldsetRect.left + fieldsetRect.width / 2, fieldsetRect.top + fieldsetRect.height / 2).id,
                document.elementFromPoint(fieldsetRect.left + 5, fieldsetRect.top + 5).id || 'other'
            ].join('|');
        ");

        Assert.Contains("fieldsetDiv|fieldset|other", result);
    }

    [Fact]
    public void Document_HitTesting_Extends_List_Items_To_Outside_Markers()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            document.body.innerHTML = '<ul style=""font-size:10px;margin:40px 0 0 40px""><li id=""outsideText"">Outside 1</li><li id=""outsideImage"" style=""list-style-image:url(data:image/gif;base64,R0lGODlhAQABAAAAACw=)"">Outside 2</li></ul><ul style=""font-size:10px;margin:20px 0 0 40px;list-style-position:inside""><li id=""insideText"">Inside 1</li></ul><pre id=""result""></pre>';

            function findOutsideMarkerHit(id) {
                var li = document.getElementById(id);
                var bounds = li.getBoundingClientRect();
                var y = (bounds.top + bounds.bottom) / 2;
                for (var x = bounds.left - 40; x < bounds.left; x++) {
                    var hit = document.elementFromPoint(x, y);
                    if (hit === li)
                        return x;
                }

                return null;
            }

            var insideBounds = document.getElementById('insideText').getBoundingClientRect();
            document.getElementById('result').textContent = [
                findOutsideMarkerHit('outsideText') !== null ? 'outsideText' : 'miss',
                findOutsideMarkerHit('outsideImage') !== null ? 'outsideImage' : 'miss',
                document.elementFromPoint(insideBounds.left + 1, (insideBounds.top + insideBounds.bottom) / 2).id
            ].join('|');
        ");

        Assert.Contains("outsideText|outsideImage|insideText", result);
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
    public void Element_Scroll_Ignores_Elements_Without_A_Scrolling_Box()
    {
        var result = ExecJs(@"
            function makeContainer(overflow) {
                var container = document.createElement('div');
                container.style.width = '100px';
                container.style.height = '100px';
                if (overflow) {
                    container.style.overflow = overflow;
                }

                var content = document.createElement('div');
                content.style.width = '250px';
                content.style.height = '250px';
                container.appendChild(content);
                document.body.appendChild(container);
                return container;
            }

            var hidden = makeContainer('hidden');
            hidden.scroll(40, 50);

            var visible = makeContainer('visible');
            visible.scroll(40, 50);

            var implicitVisible = makeContainer('');
            implicitVisible.scrollLeft = 40;
            implicitVisible.scrollTop = 50;

            document.getElementById('result').textContent = [
                hidden.scrollLeft + ',' + hidden.scrollTop,
                visible.scrollLeft + ',' + visible.scrollTop,
                implicitVisible.scrollLeft + ',' + implicitVisible.scrollTop
            ].join('|');
        ");

        Assert.Contains("40,50|0,0|0,0", result);
    }

    [Fact]
    public void Element_ScrollParent_Finds_Nearest_Relevant_Scroll_Container()
    {
        var result = ExecJs(@"
            function append(tag, parent, id, style) {
                var el = document.createElement(tag);
                if (id) el.id = id;
                if (style) el.style.cssText = style;
                parent.appendChild(el);
                return el;
            }

            var childOfRoot = append('div', document.body, 'childOfRoot');
            var scroller3 = append('div', document.body, 'scroller3', 'overflow:scroll; height:100px;');
            var fixedToRoot = append('div', scroller3, 'fixedToRoot', 'position:fixed;');
            var transformed = append('div', scroller3, null, 'transform:scale(1);');
            var scroller2 = append('div', transformed, 'scroller2', 'overflow:scroll; height:100px;');
            var relpos = append('div', scroller2, null, 'position:relative;');
            var scroller1 = append('div', relpos, 'scroller1', 'overflow:scroll; height:100px;');
            var wrapper = append('div', scroller1);
            var normalChild = append('div', wrapper, 'normalChild');
            var noBox = append('div', wrapper, 'noBox', 'display:none;');
            var absPosChild = append('div', wrapper, 'absPosChild', 'position:absolute;');
            var fixedPosChild = append('div', wrapper, 'fixedPosChild', 'position:fixed;');
            var hidden = append('div', scroller1, 'hidden', 'overflow:hidden;');
            var childOfHidden = append('div', hidden, 'childOfHidden');
            var contents = append('div', scroller1, null, 'display:contents;');
            var childOfDisplayContents = append('div', contents, 'childOfDisplayContents');

            document.getElementById('result').textContent = [
                normalChild.scrollParent().id,
                childOfHidden.scrollParent().id,
                noBox.scrollParent() === null,
                absPosChild.scrollParent().id,
                fixedPosChild.scrollParent().id,
                fixedToRoot.scrollParent() === null,
                childOfRoot.scrollParent() === document.scrollingElement,
                childOfDisplayContents.scrollParent().id,
                document.body.scrollParent() === document.scrollingElement,
                document.documentElement.scrollParent() === null,
                document.scrollingElement.scrollParent() === null
            ].join('|');
        ");

        Assert.Contains("scroller1|hidden|true|scroller2|scroller3|true|true|scroller1|true|true|true", result);
    }

    [Fact]
    public void Element_ScrollParent_Crosses_Open_And_Closed_Shadow_Roots()
    {
        var result = ExecJs(@"
            function append(tag, parent, id, style) {
                var el = document.createElement(tag);
                if (id) el.id = id;
                if (style) el.style.cssText = style;
                parent.appendChild(el);
                return el;
            }

            var outerScroller = append('div', document.body, 'outerScroller', 'overflow:scroll; height:150px;');
            var spacer = append('div', outerScroller, null, 'height:1000px;');

            var closedHost = append('div', spacer, 'closedHost');
            var closedWrapper = append('div', closedHost);
            var closedInner = append('div', closedWrapper, 'closedInner', 'height:1000px;');
            var closedShadowRoot = closedHost.attachShadow({ mode: 'closed' });
            var closedShadowOuter = append('div', closedShadowRoot, 'closedShadowOuter');
            var closedShadowScroller = append('div', closedShadowOuter, null, 'overflow:scroll; height:50px;');

            var openHost = append('div', spacer, 'openHost');
            var openWrapper = append('div', openHost);
            var openInner = append('div', openWrapper, 'openInner', 'height:1000px;');
            var openShadowRoot = openHost.attachShadow({ mode: 'open' });
            var openShadowOuter = append('div', openShadowRoot, 'openShadowOuter');
            var openShadowScroller = append('div', openShadowOuter, null, 'overflow:scroll; height:50px;');

            document.getElementById('result').textContent = [
                closedInner.scrollParent().id,
                openInner.scrollParent().id,
                closedShadowRoot.querySelector('#closedShadowOuter').scrollParent().id,
                openHost.shadowRoot.querySelector('#openShadowOuter').scrollParent().id,
                closedHost.shadowRoot === null,
                openHost.shadowRoot === openShadowRoot
            ].join('|');
        ");

        Assert.Contains("outerScroller|outerScroller|outerScroller|outerScroller|true|true", result);
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
    public void MatchMedia_Clamps_Negative_Calc_Lengths_To_Zero()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent =
                'MATCH:' + window.matchMedia('(min-width: calc(-100px))').matches;
        ");
        Assert.Contains("MATCH:true", result);
    }

    [Fact]
    public void Viewport_Calc_Lengths_Resolve_In_BoundingClientRect()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            var target = document.createElement('div');
            target.style.position = 'absolute';
            target.style.width = 'calc(100vw + 50px)';
            target.style.height = 'calc(100vh + 50px)';
            target.style.left = '-50px';
            target.style.top = '-50px';
            document.body.appendChild(target);

            var rect = target.getBoundingClientRect();
            document.getElementById('result').textContent =
                'RECT:' + rect.left + ',' + rect.top + ',' + rect.width + ',' + rect.height;
        ");
        Assert.Contains("RECT:-50,-50,1074,818", result);
    }

    [Fact]
    public void Viewport_Calc_Lengths_With_Percentages_Resolve_In_BoundingClientRect()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            var target = document.createElement('div');
            target.style.position = 'absolute';
            target.style.width = 'calc(100vw + 50%)';
            target.style.height = 'calc(100vh + 50%)';
            target.style.left = '-50%';
            target.style.top = '-50%';
            document.body.appendChild(target);

            var rect = target.getBoundingClientRect();
            document.getElementById('result').textContent =
                'RECT:' + rect.left + ',' + rect.top + ',' + rect.width + ',' + rect.height;
        ");
        Assert.Contains("RECT:-512,-384,1536,1152", result);
    }

    [Fact]
    public void Viewport_Lengths_Can_Be_Explicitly_Inherited_And_Feed_Rem_And_Em_Font_Sizes()
    {
        var result = CaptureService.ExecuteScriptsWithDom(@"<!doctype html>
<html><head><title>Test</title>
<style>
html, body { margin:0; padding:0; }
#outer { position:relative; background:green; width:50vw; height:100vh; }
#inner { position:absolute; background:green; left:100%; width:inherit; height:inherit; }
html { font-size:100vw; }
#target { background:green; width:1rem; height:1em; font-size:100vh; }
</style>
</head>
<body>
<div id=""result""></div>
<div id=""outer""><div id=""inner""></div></div>
<div id=""target""></div>
<script>
var outerRect = document.getElementById('outer').getBoundingClientRect();
var innerRect = document.getElementById('inner').getBoundingClientRect();
var targetRect = document.getElementById('target').getBoundingClientRect();
document.getElementById('result').textContent =
  [outerRect.width, outerRect.height, innerRect.left, innerRect.width, innerRect.height, targetRect.width, targetRect.height].join(',');
</script>
</body></html>", "https://www.google.com");
        Assert.Contains("512,768,512,512,768,1024,768", result);
    }

    [Fact]
    public void Viewport_Lengths_Interpolate_In_Animation_Snapshots()
    {
        var result = CaptureService.ExecuteScriptsWithDom(@"<!doctype html>
<html><head><title>Test</title>
<style>
@keyframes vhAnim {
  from { width: 75vw; height: 75vh; }
  to   { width: 125vw; height: 125vh; }
}
@keyframes mixedAnim {
  from { width: 0%; height: 0%; }
  to   { width: 200vw; height: 200vh; }
}
html, body { margin:0; padding:0; height:100%; }
.box { position:relative; background:green; }
#vh-box { animation: vhAnim 2000000s linear; animation-delay: -1000000s; }
#mixed-box { animation: mixedAnim 2000000s linear; animation-delay: -1000000s; }
</style>
</head>
<body>
<div id=""result""></div>
<div id=""vh-box"" class=""box""></div>
<div id=""mixed-box"" class=""box""></div>
<script>
var vhRect = document.getElementById('vh-box').getBoundingClientRect();
var mixedRect = document.getElementById('mixed-box').getBoundingClientRect();
document.getElementById('result').textContent =
  [vhRect.width, vhRect.height, mixedRect.width, mixedRect.height].join(',');
</script>
</body></html>", "https://www.google.com");
        Assert.Contains("id=\"vh-box\" class=\"box\" style=\"width: 1024px; height: 768px\"", result);
        Assert.Contains("id=\"mixed-box\" class=\"box\" style=\"width: 1024px; height: 768px\"", result);
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
        Assert.Contains("950,920", result);
    }

    [Fact]
    public void ScrollIntoView_Scales_Zoomed_Target_ScrollMargin_In_Scroller_Coordinates()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function buildTarget(useInheritedMargin) {
                var container = document.createElement('div');
                container.style.width = '200px';
                container.style.height = '100px';
                container.style.overflowX = 'hidden';
                container.style.overflowY = 'scroll';
                container.style.paddingTop = '40px';
                container.style.paddingBottom = '40px';
                if (useInheritedMargin) {
                    container.style.scrollMarginTop = '20px';
                }

                var buffer = document.createElement('div');
                buffer.style.height = '300px';
                var target = document.createElement('div');
                target.style.height = '10px';
                target.style.width = '200px';
                target.style.zoom = '2';
                target.style.scrollMarginTop = useInheritedMargin ? 'inherit' : '20px';
                var tail = document.createElement('div');
                tail.style.height = '300px';

                container.appendChild(buffer);
                container.appendChild(target);
                container.appendChild(tail);
                document.body.appendChild(container);
                target.scrollIntoView();
                return container.scrollTop;
            }

            document.getElementById('result').textContent =
                buildTarget(false) + ',' + buildTarget(true);
        ");

        Assert.Contains("300,300", result);
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
                target.scrollIntoView({ block: 'start', inline: 'start' });
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
                target.scrollIntoView({ block: 'start', inline: 'start' });
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
    public void ScrollIntoView_Converts_Inherited_ScrollPadding_From_Owner_To_Zoomed_Scroller_Coordinates()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function buildContainer(zoomed) {
                if (!zoomed) {
                    var container = document.createElement('div');
                    container.style.width = '120px';
                    container.style.height = '100px';
                    container.style.overflow = 'hidden';
                    container.style.border = '1px solid black';
                    container.style.scrollPaddingTop = '20px';

                    var before = document.createElement('div');
                    before.style.height = '1000px';
                    var target = document.createElement('div');
                    target.style.height = '20px';
                    var after = document.createElement('div');
                    after.style.height = '1000px';

                    container.appendChild(before);
                    container.appendChild(target);
                    container.appendChild(after);
                    document.body.appendChild(container);
                    target.scrollIntoView({ block: 'start', inline: 'start' });
                    return String(container.scrollTop);
                }

                var owner = document.createElement('div');
                owner.style.display = 'inline-block';
                owner.style.scrollPaddingTop = '20px';

                var zoomedContainer = document.createElement('div');
                zoomedContainer.style.width = '120px';
                zoomedContainer.style.height = '100px';
                zoomedContainer.style.overflow = 'hidden';
                zoomedContainer.style.border = '1px solid black';
                zoomedContainer.style.scrollPaddingTop = 'inherit';
                zoomedContainer.style.zoom = '2';

                var zoomedBefore = document.createElement('div');
                zoomedBefore.style.height = '1000px';
                var zoomedTarget = document.createElement('div');
                zoomedTarget.style.height = '20px';
                var zoomedAfter = document.createElement('div');
                zoomedAfter.style.height = '1000px';

                zoomedContainer.appendChild(zoomedBefore);
                zoomedContainer.appendChild(zoomedTarget);
                zoomedContainer.appendChild(zoomedAfter);
                owner.appendChild(zoomedContainer);
                document.body.appendChild(owner);
                zoomedTarget.scrollIntoView({ block: 'start', inline: 'start' });
                return String(zoomedContainer.scrollTop);
            }

            document.getElementById('result').textContent =
                buildContainer(false) + '|' + buildContainer(true);
        ");

        Assert.Equal("980|980", result);
    }

    [Fact]
    public void ScrollIntoView_Honors_Block_And_Inline_Options_In_Raw_Css_Pixels()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function buildContainer(zoom) {
                var container = document.createElement('div');
                container.style.position = 'relative';
                container.style.width = '140px';
                container.style.height = '120px';
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
                target.scrollIntoView({ block: 'center', inline: 'end' });
                return container.scrollLeft + ',' + container.scrollTop;
            }

            document.getElementById('result').textContent =
                buildContainer('1') + '|' + buildContainer('2');
        ");

        Assert.Contains("180,190|180,190", result);
    }

    [Fact]
    public void ScrollIntoView_Legacy_False_Aligns_To_Block_End()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            var container = document.createElement('div');
            container.style.position = 'relative';
            container.style.width = '140px';
            container.style.height = '120px';
            container.style.overflow = 'auto';

            var content = document.createElement('div');
            content.style.width = '200px';
            content.style.height = '600px';

            var target = document.createElement('div');
            target.style.position = 'absolute';
            target.style.left = '20px';
            target.style.top = '240px';
            target.style.width = '20px';
            target.style.height = '20px';

            container.appendChild(content);
            container.appendChild(target);
            document.body.appendChild(container);
            target.scrollIntoView(false);

            document.getElementById('result').textContent =
                container.scrollLeft + ',' + container.scrollTop;
        ");

        Assert.Contains("0,140", result);
    }

    [Fact]
    public void ScrollIntoView_Defaults_To_InlineNearest_For_Omitted_Options_And_Boolean_Overloads()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            var container = document.createElement('div');
            container.style.position = 'relative';
            container.style.width = '120px';
            container.style.height = '100px';
            container.style.overflow = 'auto';

            var content = document.createElement('div');
            content.style.width = '400px';
            content.style.height = '400px';
            content.style.position = 'relative';

            var target = document.createElement('div');
            target.style.position = 'absolute';
            target.style.left = '60px';
            target.style.top = '300px';
            target.style.width = '20px';
            target.style.height = '20px';

            container.appendChild(content);
            container.appendChild(target);
            document.body.appendChild(container);

            function run(mode) {
                container.scrollLeft = 40;
                container.scrollTop = 0;
                if (mode === 'default') {
                    target.scrollIntoView();
                } else if (mode === 'true') {
                    target.scrollIntoView(true);
                } else {
                    target.scrollIntoView(false);
                }

                return container.scrollLeft + ',' + container.scrollTop;
            }

            document.getElementById('result').textContent =
                run('default') + '|' + run('true') + '|' + run('false');
        ");

        Assert.Contains("40,300|40,300|40,220", result);
    }

    [Fact]
    public void Window_Scroll_APIs_Update_Root_Scroll_Offsets_And_VisualViewport()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            document.body.style.width = '2000px';
            document.body.style.height = '4000px';
            visualViewport.scale = 2;

            var events = 0;
            visualViewport.addEventListener('scroll', function() { events++; });
            window.scrollTo({ left: 40, top: 1000 });
            window.scrollBy({ left: 10, top: 15 });

            document.getElementById('result').textContent = [
                window.scrollX,
                window.scrollY,
                window.pageXOffset,
                window.pageYOffset,
                document.scrollingElement.scrollLeft,
                document.scrollingElement.scrollTop,
                visualViewport.pageLeft,
                visualViewport.pageTop,
                events
            ].join('|');
        ");

        Assert.Contains("50|1015|50|1015|50|1015|50|1015|2", result);
    }

    [Fact]
    public void VisualViewport_ScrollIntoView_Fixed_Target_Uses_Visual_Page_Offset()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            document.body.style.height = '4000px';
            visualViewport.scale = 2;
            window.scrollTo(0, 1000);

            var fixed = document.createElement('div');
            fixed.style.position = 'fixed';
            fixed.style.bottom = '0';
            fixed.style.left = '0';
            fixed.style.width = '100px';
            fixed.style.height = '60px';
            fixed.style.overflow = 'auto';

            var spacer = document.createElement('div');
            spacer.style.height = '500px';

            var target = document.createElement('input');
            target.style.display = 'block';
            target.style.height = '20px';

            fixed.appendChild(spacer);
            fixed.appendChild(target);
            document.body.appendChild(fixed);

            var before = visualViewport.pageTop;
            var fired = false;
            visualViewport.addEventListener('scroll', function() { fired = true; });
            target.scrollIntoView({ behavior: 'instant' });

            document.getElementById('result').textContent = [
                window.scrollY,
                before,
                visualViewport.pageTop,
                window.pageYOffset,
                fired,
                visualViewport.scale,
                visualViewport.height
            ].join('|');
        ");

        Assert.Contains("1000|1000|1384|1000|true|2|384", result);
    }

    [Fact]
    public void ScrollIntoView_Does_Not_Scroll_Root_For_Targets_Inside_Unscrollable_Fixed_Containers()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            document.body.style.width = '2000px';
            document.body.style.height = '2000px';

            var container = document.createElement('div');
            container.style.position = 'fixed';
            container.style.left = '10px';
            container.style.bottom = '10px';
            container.style.width = '150px';
            container.style.height = '150px';

            var target = document.createElement('div');
            target.style.position = 'absolute';
            target.style.left = '50%';
            target.style.top = '50%';
            target.style.width = '10px';
            target.style.height = '10px';

            container.appendChild(target);
            document.body.appendChild(container);
            target.scrollIntoView();

            document.getElementById('result').textContent =
                document.documentElement.scrollLeft + ',' + document.documentElement.scrollTop;
        ");
        Assert.Contains("0,0", result);
    }

    [Fact]
    public void ScrollIntoView_Scrolls_Fixed_Scrollers_Without_Bubbling_To_The_Root()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            document.body.style.width = '2000px';
            document.body.style.height = '2000px';

            var container = document.createElement('div');
            container.style.position = 'fixed';
            container.style.right = '10px';
            container.style.bottom = '10px';
            container.style.width = '150px';
            container.style.height = '150px';
            container.style.overflow = 'auto';

            var filler = document.createElement('div');
            filler.style.width = '600px';
            filler.style.height = '600px';

            var target = document.createElement('div');
            target.style.position = 'absolute';
            target.style.left = '200%';
            target.style.top = '200%';
            target.style.width = '10px';
            target.style.height = '10px';

            container.appendChild(filler);
            container.appendChild(target);
            document.body.appendChild(container);
            target.scrollIntoView({ block: 'start', inline: 'start' });

            document.getElementById('result').textContent =
                document.documentElement.scrollLeft + ',' + document.documentElement.scrollTop + '|' +
                container.scrollLeft + ',' + container.scrollTop;
        ");
        Assert.Contains("0,0|300,300", result);
    }

    [Fact]
    public void ScrollIntoView_Clamps_Fixed_Scrollers_To_Their_Scroll_Bounds()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';
            document.body.style.width = '2000px';
            document.body.style.height = '2000px';

            var container = document.createElement('div');
            container.style.position = 'fixed';
            container.style.right = '10px';
            container.style.bottom = '10px';
            container.style.width = '150px';
            container.style.height = '150px';
            container.style.overflow = 'auto';

            var target = document.createElement('div');
            target.style.position = 'absolute';
            target.style.left = '200%';
            target.style.top = '200%';
            target.style.width = '10px';
            target.style.height = '10px';

            container.appendChild(target);
            document.body.appendChild(container);
            target.scrollIntoView();

            document.getElementById('result').textContent =
                document.documentElement.scrollLeft + ',' + document.documentElement.scrollTop + '|' +
                container.scrollLeft + ',' + container.scrollTop + '|' +
                container.scrollWidth + ',' + container.clientWidth;
        ");

        Assert.Contains("0,0|160,160|310,150", result);
    }

    [Fact]
    public void ScrollIntoView_Does_Not_Scroll_Hidden_Root_For_Zoomed_Scrollers()
    {
        var result = ExecJs(@"
            document.documentElement.style.overflow = 'hidden';
            document.body.style.margin = '0';
            document.body.style.overflow = 'hidden';

            function buildContainer(zoom) {
                var container = document.createElement('div');
                container.style.position = 'relative';
                container.style.display = 'inline-block';
                container.style.width = '120px';
                container.style.height = '100px';
                container.style.overflow = 'auto';
                container.style.border = '1px solid black';
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
                return [
                    document.documentElement.scrollLeft,
                    document.documentElement.scrollTop,
                    container.scrollLeft,
                    container.scrollTop
                ].join(',');
            }

            document.getElementById('result').textContent =
                buildContainer('1') + '|' + buildContainer('2');
        ");

        Assert.Contains("0,0,300,240|0,0,300,240", result);
    }

    [Fact]
    public void ScrollIntoView_Treats_Assigned_Slot_As_Scroll_Container()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            var host = document.createElement('div');
            var spacer = document.createElement('div');
            spacer.style.height = '200px';
            var target = document.createElement('div');
            target.style.height = '100px';
            target.style.width = '100px';

            host.appendChild(spacer);
            host.appendChild(target);
            document.body.appendChild(host);

            var shadow = host.attachShadow({ mode: 'open' });
            var slot = document.createElement('slot');
            slot.style.display = 'block';
            slot.style.overflow = 'hidden';
            slot.style.width = '100px';
            slot.style.height = '100px';
            shadow.appendChild(slot);

            target.scrollIntoView();

            document.getElementById('result').textContent = [
                slot.scrollTop,
                slot.scrollHeight,
                slot.clientHeight,
                host.scrollTop
            ].join('|');
        ");

        Assert.Contains("200|300|100|0", result);
    }

    [Fact]
    public void ScrollIntoView_Maps_Block_And_Inline_Axes_For_WritingModes()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function measure(writingMode, direction, options) {
                var scroller = document.createElement('div');
                scroller.style.overflow = 'scroll';
                scroller.style.width = '300px';
                scroller.style.height = '300px';
                scroller.style.position = 'relative';
                if (writingMode) {
                    scroller.style.writingMode = writingMode;
                }
                if (direction) {
                    scroller.style.direction = direction;
                }

                var content = document.createElement('div');
                content.style.width = '600px';
                content.style.height = '600px';

                var target = document.createElement('div');
                target.style.position = 'absolute';
                target.style.left = '200px';
                target.style.top = '200px';
                target.style.width = '200px';
                target.style.height = '200px';

                scroller.appendChild(content);
                scroller.appendChild(target);
                document.body.appendChild(scroller);
                target.scrollIntoView(options);
                return scroller.scrollLeft + ',' + scroller.scrollTop;
            }

            document.getElementById('result').textContent = [
                measure('horizontal-tb', 'rtl', { block: 'start', inline: 'start' }),
                measure('vertical-rl', 'ltr', { block: 'center', inline: 'end' }),
                measure('sideways-rl', 'rtl', { block: 'end', inline: 'center' })
            ].join('|');
        ");

        Assert.Contains("-200,200|-150,100|-100,-150", result);
    }

    [Fact]
    public void SmoothScroll_On_OverflowHidden_Element_Can_Be_Interrupted_By_Scroll_Handler()
    {
        using var ctx = new Broiler.JavaScript.Engine.JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, "<!DOCTYPE html><html><body></body></html>", "file:///test.html");

        var result = ctx.Eval("""
            (() => {
                var scroller = document.createElement('div');
                scroller.style.overflowY = 'hidden';
                scroller.style.width = '100px';
                scroller.style.height = '100px';
                scroller.style.scrollBehavior = 'smooth';

                function block() {
                    var d = document.createElement('div');
                    d.style.width = '100px';
                    d.style.height = '100px';
                    return d;
                }

                scroller.appendChild(block());
                scroller.appendChild(block());
                scroller.appendChild(block());
                document.body.appendChild(scroller);

                var interrupted = 0;
                var scrollEvents = 0;
                var scrollEnds = 0;
                scroller.onscroll = function () {
                    scrollEvents++;
                    if (scroller.scrollTop > 1 && scroller.scrollTop < 200) {
                        scroller.scrollTop = 1;
                        interrupted++;
                    }
                };
                scroller.onscrollend = function () {
                    scrollEnds++;
                };

                scroller.scrollTop = 200;
                return [scroller.scrollTop, interrupted, scrollEvents, scrollEnds].join('|');
            })()
            """);

        Assert.Equal("1|1|2|1", result.ToString());
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
    public void FontRelative_Ic_Units_Resolve_To_Raw_Css_Pixels_Under_Zoom()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function measure(zoom) {
                var box = document.createElement('div');
                box.style.width = '5ic';
                box.style.height = '10ic';
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
        Assert.Contains("80,160|80,160", result);
    }

    [Fact]
    public void Attr_Lengths_Resolve_In_Direct_And_Max_Length_Cases()
    {
        var result = ExecJs(@"
            document.body.style.margin = '0';

            function addBox(id, attrValue) {
                var box = document.createElement('div');
                box.id = id;
                box.style.position = 'absolute';
                box.style.height = '20px';
                box.setAttribute('data-test', attrValue);
                document.body.appendChild(box);
                return box;
            }

            var valid = addBox('valid', '200px');
            valid.style.width = 'attr(data-test type(<length>))';
            var fallback = addBox('fallback', 'qqffuutt');
            fallback.style.top = '30px';
            fallback.style.width = 'attr(data-test type(<length>), 200px)';
            var maxed = addBox('maxed', '200px');
            maxed.style.top = '60px';
            maxed.style.width = 'max(attr(data-test type(<length>)))';

            document.getElementById('result').textContent =
                valid.offsetWidth + ',' + fallback.offsetWidth + ',' + maxed.offsetWidth;
        ");
        Assert.Contains("200,200,200", result);
    }

    [Fact]
    public void Zoom_CssomView_Geometry_APIs_Use_Raw_Css_Pixels()
    {
        var result = ExecJs(@"
            document.body.style.margin = '8px';

            function makeDiv(id, width, height, zoom, className) {
                var div = document.createElement('div');
                div.id = id;
                div.style.width = width;
                div.style.height = height;
                div.style.backgroundColor = 'blue';
                if (zoom) div.style.zoom = zoom;
                if (className) div.className = className;
                document.body.appendChild(div);
                return div;
            }

            var noZoom = makeDiv('no_zoom', '64px', '64px');
            var withZoom = makeDiv('with_zoom', '64px', '64px', '4');

            var outer = document.createElement('div');
            outer.style.zoom = '2';
            var nested = makeDiv('nested_zoom', '64px', '64px', '4');
            outer.appendChild(nested);
            document.body.appendChild(outer);

            var transformAndZoom = makeDiv('transform_and_zoom', '64px', '64px', '4');
            transformAndZoom.style.transform = 'scale(2)';
            transformAndZoom.style.transformOrigin = 'top left';

            var noZoomRect = noZoom.getBoundingClientRect();
            var withZoomRect = withZoom.getBoundingClientRect();
            var nestedZoomRect = nested.getClientRects()[0];
            var transformZoomRect = transformAndZoom.getBoundingClientRect();

            document.getElementById('result').textContent =
                [noZoomRect.left, noZoomRect.top, noZoomRect.width, noZoomRect.height].join(',') + '|' +
                [withZoomRect.left, withZoomRect.top, withZoomRect.width, withZoomRect.height].join(',') + '|' +
                [nestedZoomRect.left, nestedZoomRect.top, nestedZoomRect.width, nestedZoomRect.height].join(',') + '|' +
                [transformZoomRect.width, transformZoomRect.height].join(',');
        ");
        Assert.Contains("8,8,64,64|8,72,256,256|8,328,512,512|512,512", result);
    }

    [Fact]
    public void Zoom_Client_Scroll_And_Offset_Metrics_Stay_In_Raw_Css_Pixels()
    {
        var result = ExecJs(@"
            document.body.style.margin = '8px';
            try {
                function makeContainer(id, zoom, childZoom) {
                    var container = document.createElement('div');
                    container.id = id;
                    container.style.width = '100px';
                    container.style.height = '100px';
                    container.style.overflow = 'scroll';
                    if (zoom) container.style.zoom = zoom;

                    var child = document.createElement('div');
                    child.style.width = '250px';
                    child.style.height = '250px';
                    if (childZoom) child.style.zoom = childZoom;
                    container.appendChild(child);
                    document.body.appendChild(container);
                    return container;
                }

                var noZoom = makeContainer('no_zoom_container', '', '');
                var zoomed = makeContainer('zoomed_container', '4', '');
                var zoomedContent = makeContainer('zoomed_content_container', '', '2');

                noZoom.scrollTo(noZoom.scrollWidth / 2, noZoom.scrollHeight / 2);
                zoomed.scrollTo(zoomed.scrollWidth / 2, zoomed.scrollHeight / 2);

                var outer = document.createElement('div');
                outer.style.zoom = '3';
                outer.style.position = 'relative';
                outer.style.width = '100px';
                outer.style.height = '100px';
                outer.style.margin = '10px';
                outer.style.border = '1px solid black';

                var rel = document.createElement('div');
                rel.style.position = 'relative';
                rel.style.top = '10px';
                rel.style.left = '10px';
                rel.style.width = '10px';
                rel.style.height = '10px';
                rel.style.margin = '1px';
                outer.appendChild(rel);

                var abs = document.createElement('div');
                abs.style.position = 'absolute';
                abs.style.top = '20px';
                abs.style.left = '20px';
                abs.style.zoom = '2';
                abs.style.width = '10px';
                abs.style.height = '10px';
                abs.style.margin = '1px';
                outer.appendChild(abs);

                document.body.appendChild(outer);

                document.getElementById('result').textContent =
                    zoomed.clientWidth + ',' +
                    zoomed.clientHeight + ',' +
                    noZoom.scrollWidth + ',' +
                    zoomed.scrollWidth + ',' +
                    zoomedContent.scrollWidth + ',' +
                    noZoom.scrollTop + ',' +
                    zoomed.scrollTop + ',' +
                    rel.offsetTop + ',' +
                    rel.offsetLeft + ',' +
                    abs.offsetTop + ',' +
                    abs.offsetLeft;
            } catch (e) {
                document.getElementById('result').textContent = 'ERR:' + e;
            }
        ");
        Assert.Contains("100,100,250,250,500,125,125,11,11,21,21", result);
    }

    [Fact]
    public void OffsetTopLeft_Are_Measured_From_OffsetParent_Padding_Edge()
    {
        var result = ExecJs(@"
            try {
                document.body.style.margin = '0';

                function createCase(display, writingMode, tagName) {
                    return createConfiguredCase(display, writingMode, tagName, '2px 10px', 'border-box', 10, 2) &&
                           createConfiguredCase(display, writingMode, tagName, '7px 4px', 'content-box', 4, 7);
                }

                function createConfiguredCase(display, writingMode, tagName, padding, boxSizing, expectedLeft, expectedTop) {
                    var container = document.createElement('div');
                    container.style.position = 'relative';
                    container.style.font = '20px/1 monospace';
                    container.style.width = '150px';
                    container.style.height = '100px';
                    container.style.padding = padding;
                    container.style.borderStyle = 'solid';
                    container.style.borderWidth = '3px 6px';
                    container.style.boxSizing = boxSizing;
                    container.style.display = display;
                    container.style.writingMode = writingMode;

                    var target = document.createElement(tagName);
                    target.textContent = 'x';
                    container.appendChild(target);
                    document.body.appendChild(container);
                    return target.offsetLeft === expectedLeft && target.offsetTop === expectedTop;
                }

                var displays = ['block', 'inline-block', 'grid', 'inline-grid', 'flex', 'inline-flex', 'flow-root'];
                var writingModes = ['horizontal-tb', 'vertical-lr'];
                var tags = ['span', 'div'];
                var passed = true;

                for (var i = 0; i < displays.length; i++) {
                    for (var j = 0; j < writingModes.length; j++) {
                        for (var k = 0; k < tags.length; k++) {
                            passed = createCase(displays[i], writingModes[j], tags[k]) && passed;
                        }
                    }
                }

                document.getElementById('result').textContent = String(passed);
            } catch (e) {
                document.getElementById('result').textContent = 'ERR:' + e;
            }
        ");
        Assert.Contains("true", result);
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
