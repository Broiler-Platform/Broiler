using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class DomBridgeTests
{
    private readonly ScriptEngine _engine = new();

    [Fact]
    public void Execute_WithHtml_DocumentTitleIsAccessible()
    {
        var html = "<html><head><title>Test Page</title></head><body></body></html>";
        var result = _engine.Execute(["var t = document.title;"], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_GetElementByIdReturnsElement()
    {
        var html = "<html><body><div id=\"main\">Hello</div></body></html>";
        var result = _engine.Execute(["var el = document.getElementById('main');"], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_GetElementByIdReturnsNullForMissing()
    {
        var html = "<html><body><div id=\"main\">Hello</div></body></html>";
        var result = _engine.Execute(
            ["var el = document.getElementById('missing'); if (el !== null) throw new Error('expected null');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_GetElementsByTagNameReturnsMatches()
    {
        var html = "<html><body><p id=\"a\">One</p><p id=\"b\">Two</p></body></html>";
        var result = _engine.Execute(
            ["var ps = document.getElementsByTagName('p'); if (ps.length < 1) throw new Error('expected elements');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_CreateElementReturnsNewElement()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["var el = document.createElement('span'); if (el.tagName !== 'SPAN') throw new Error('wrong tag');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_CreateElementWithoutArgReturnsFalse()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(["document.createElement();"], html);
        Assert.False(result);
    }

    [Fact]
    public void Execute_WithHtml_VoidElementsAreParsed()
    {
        var html = "<html><body><input id=\"field\"><img id=\"logo\" /></body></html>";
        var result = _engine.Execute(
            ["var el = document.getElementById('field'); if (el === null) throw new Error('expected input element');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_ElementHasExpectedProperties()
    {
        var html = "<html><body><div id=\"test\" class=\"box\">Content</div></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.getElementById('test');
                if (el.tagName !== 'DIV') throw new Error('wrong tagName');
                if (el.id !== 'test') throw new Error('wrong id');
                if (el.className !== 'box') throw new Error('wrong className');
                if (el.innerHTML !== 'Content') throw new Error('wrong innerHTML');
            " ],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_EmptyScriptsReturnsTrue()
    {
        var result = _engine.Execute(Array.Empty<string>(), "<html></html>");
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_InvalidScriptReturnsFalse()
    {
        var result = _engine.Execute(["invalid js @@!!"], "<html></html>");
        Assert.False(result);
    }

    [Fact]
    public void DomBridge_ParsesTitle()
    {
        var bridge = new DomBridge();
        using var context = new YantraJS.Core.JSContext();
        bridge.Attach(context, "<html><head><title>My Title</title></head></html>");

        Assert.Equal("My Title", bridge.Title);
    }

    [Fact]
    public void DomBridge_ParsesElements()
    {
        var bridge = new DomBridge();
        using var context = new YantraJS.Core.JSContext();
        bridge.Attach(context, "<html><body><div id=\"a\" class=\"c\">Text</div></body></html>");

        Assert.True(bridge.Elements.Count > 0);
        var div = bridge.Elements[0];
        Assert.Equal("div", div.TagName);
        Assert.Equal("a", div.Id);
        Assert.Equal("c", div.ClassName);
        Assert.Equal("Text", div.InnerHtml);
    }

    [Fact]
    public void DomBridge_NoTitle_ReturnsEmpty()
    {
        var bridge = new DomBridge();
        using var context = new YantraJS.Core.JSContext();
        bridge.Attach(context, "<html><body></body></html>");

        Assert.Equal(string.Empty, bridge.Title);
    }

    // ------------------------------------------------------------------
    //  getElementsByClassName
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_GetElementsByClassNameReturnsMatches()
    {
        var html = "<html><body><div class=\"box\">A</div><p class=\"box\">B</p><span class=\"other\">C</span></body></html>";
        var result = _engine.Execute(
            ["var els = document.getElementsByClassName('box'); if (els.length < 1) throw new Error('expected elements');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_GetElementsByClassNameReturnsEmptyForMissing()
    {
        var html = "<html><body><div class=\"box\">A</div></body></html>";
        var result = _engine.Execute(
            ["var els = document.getElementsByClassName('missing'); if (els.length !== 0) throw new Error('expected empty');"],
            html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  querySelector / querySelectorAll
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_QuerySelectorByTag()
    {
        var html = "<html><body><p id=\"first\">One</p><p id=\"second\">Two</p></body></html>";
        var result = _engine.Execute(
            ["var el = document.querySelector('p'); if (el === null) throw new Error('expected element');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_QuerySelectorById()
    {
        var html = "<html><body><div id=\"target\">Hello</div></body></html>";
        var result = _engine.Execute(
            ["var el = document.querySelector('#target'); if (el === null) throw new Error('expected element'); if (el.id !== 'target') throw new Error('wrong id');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_QuerySelectorByClass()
    {
        var html = "<html><body><div class=\"card active\">Hello</div></body></html>";
        var result = _engine.Execute(
            ["var el = document.querySelector('.card'); if (el === null) throw new Error('expected element');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_QuerySelectorByAttribute()
    {
        var html = "<html><body><input type=\"text\" id=\"name\"></body></html>";
        var result = _engine.Execute(
            ["var el = document.querySelector('[type=text]'); if (el === null) throw new Error('expected element');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_QuerySelectorReturnsNullForNoMatch()
    {
        var html = "<html><body><div>Hello</div></body></html>";
        var result = _engine.Execute(
            ["var el = document.querySelector('#nonexistent'); if (el !== null) throw new Error('expected null');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_QuerySelectorAll()
    {
        var html = "<html><body><li class=\"item\">A</li><li class=\"item\">B</li><li class=\"other\">C</li></body></html>";
        var result = _engine.Execute(
            ["var els = document.querySelectorAll('.item'); if (els.length < 1) throw new Error('expected elements');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_QuerySelectorCompound()
    {
        var html = "<html><body><div id=\"box\" class=\"card\">Hello</div></body></html>";
        var result = _engine.Execute(
            ["var el = document.querySelector('div.card#box'); if (el === null) throw new Error('expected element');"],
            html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  element.style
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_StyleSetPropertyAndGetPropertyValue()
    {
        var html = "<html><body><div id=\"el\">Text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              el.style.setProperty('color', 'red');
              var val = el.style.getPropertyValue('color');
              if (val !== 'red') throw new Error('expected red, got ' + val);"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_StyleCssTextGetterAndSetter()
    {
        var html = "<html><body><div id=\"el\">Text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              el.style.cssText = 'color: blue; font-size: 14px';
              var color = el.style.getPropertyValue('color');
              if (color !== 'blue') throw new Error('expected blue, got ' + color);
              var size = el.style.getPropertyValue('font-size');
              if (size !== '14px') throw new Error('expected 14px, got ' + size);"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_StyleRemoveProperty()
    {
        var html = "<html><body><div id=\"el\" style=\"color: red\">Text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              el.style.removeProperty('color');
              var val = el.style.getPropertyValue('color');
              if (val !== '') throw new Error('expected empty string after remove');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void DomBridge_ParsesInlineStyle()
    {
        var bridge = new DomBridge();
        using var context = new YantraJS.Core.JSContext();
        bridge.Attach(context, "<html><body><div id=\"el\" style=\"color: red; font-size: 12px\">Text</div></body></html>");

        var el = bridge.Elements.FirstOrDefault(e => e.Id == "el");
        Assert.NotNull(el);
        Assert.True(el.Style.ContainsKey("color"));
        Assert.Equal("red", el.Style["color"]);
        Assert.True(el.Style.ContainsKey("font-size"));
    }

    // ------------------------------------------------------------------
    //  element.classList
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_ClassListContains()
    {
        var html = "<html><body><div id=\"el\" class=\"box active\">Text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              if (!el.classList.contains('box')) throw new Error('expected box');
              if (!el.classList.contains('active')) throw new Error('expected active');
              if (el.classList.contains('missing')) throw new Error('unexpected missing');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_ClassListAdd()
    {
        var html = "<html><body><div id=\"el\" class=\"box\">Text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              el.classList.add('highlight');
              if (!el.classList.contains('highlight')) throw new Error('expected highlight');
              if (!el.classList.contains('box')) throw new Error('expected box still present');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_ClassListRemove()
    {
        var html = "<html><body><div id=\"el\" class=\"box active\">Text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              el.classList.remove('active');
              if (el.classList.contains('active')) throw new Error('active should be removed');
              if (!el.classList.contains('box')) throw new Error('box should remain');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_ClassListToggleOn()
    {
        var html = "<html><body><div id=\"el\" class=\"box\">Text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              var added = el.classList.toggle('highlight');
              if (!added) throw new Error('expected true (added)');
              if (!el.classList.contains('highlight')) throw new Error('expected highlight');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_ClassListToggleOff()
    {
        var html = "<html><body><div id=\"el\" class=\"box active\">Text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              var removed = el.classList.toggle('active');
              if (removed) throw new Error('expected false (removed)');
              if (el.classList.contains('active')) throw new Error('active should be removed');"
        ], html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  element.setAttribute / getAttribute
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_GetAttributeReturnsValue()
    {
        var html = "<html><body><input id=\"field\" type=\"email\"></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('field');
              var t = el.getAttribute('type');
              if (t !== 'email') throw new Error('expected email, got ' + t);"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_GetAttributeReturnsNullForMissing()
    {
        var html = "<html><body><div id=\"el\">Text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              var val = el.getAttribute('data-missing');
              if (val !== null) throw new Error('expected null');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_SetAttribute()
    {
        var html = "<html><body><div id=\"el\">Text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              el.setAttribute('data-value', '42');
              var val = el.getAttribute('data-value');
              if (val !== '42') throw new Error('expected 42, got ' + val);"
        ], html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  Mutable className and innerHTML
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_ClassNameIsMutable()
    {
        var html = "<html><body><div id=\"el\" class=\"old\">Text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              el.className = 'new';
              if (el.className !== 'new') throw new Error('expected new');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_InnerHtmlIsMutable()
    {
        var html = "<html><body><div id=\"el\">Old content</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              el.innerHTML = '<span>New</span>';
              if (el.innerHTML !== '<span>New</span>') throw new Error('expected new innerHTML');"
        ], html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  DomElement.Attributes
    // ------------------------------------------------------------------

    [Fact]
    public void DomBridge_ParsesAttributes()
    {
        var bridge = new DomBridge();
        using var context = new YantraJS.Core.JSContext();
        bridge.Attach(context, "<html><body><input id=\"field\" type=\"checkbox\" checked></body></html>");

        var el = bridge.Elements.FirstOrDefault(e => e.Id == "field");
        Assert.NotNull(el);
        Assert.True(el.Attributes.ContainsKey("type"));
        Assert.Equal("checkbox", el.Attributes["type"]);
    }

    // ------------------------------------------------------------------
    //  window global
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_WindowIsDefined()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["if (typeof window === 'undefined') throw new Error('window is undefined');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_WindowDocumentReferenceWorks()
    {
        var html = "<html><head><title>Hello</title></head><body></body></html>";
        var result = _engine.Execute(
            ["if (window.document.title !== 'Hello') throw new Error('expected Hello');"],
            html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  window.localStorage
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_WindowLocalStorageIsDefined()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["if (typeof window.localStorage === 'undefined') throw new Error('localStorage is undefined');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_LocalStorageGetItemReturnsNull()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["if (window.localStorage.getItem('missing') !== null) throw new Error('expected null');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_LocalStorageSetAndGetItem()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
        [
            @"window.localStorage.setItem('key', 'value');
              var v = window.localStorage.getItem('key');
              if (v !== 'value') throw new Error('expected value, got ' + v);"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_LocalStorageBracketAccessReturnsUndefined()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["var v = window.localStorage['nonexistent']; if (v !== undefined) throw new Error('expected undefined, got ' + v);"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_LocalStorageRemoveItem()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
        [
            @"window.localStorage.setItem('k', 'v');
              window.localStorage.removeItem('k');
              if (window.localStorage.getItem('k') !== null) throw new Error('expected null after remove');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_LocalStorageClear()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
        [
            @"window.localStorage.setItem('a', '1');
              window.localStorage.setItem('b', '2');
              window.localStorage.clear();
              if (window.localStorage.getItem('a') !== null) throw new Error('expected null after clear');
              if (window.localStorage.getItem('b') !== null) throw new Error('expected null after clear');"
        ], html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  window.matchMedia
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_WindowMatchMediaReturnsFalseMatches()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["var mq = window.matchMedia('(prefers-color-scheme: dark)'); if (mq.matches !== false) throw new Error('expected false');"],
            html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  document.documentElement
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_DocumentElementIsDefined()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["if (!document.documentElement) throw new Error('documentElement is undefined');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_DocumentElementClassListAdd()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
        [
            @"document.documentElement.classList.add('dark');
              if (!document.documentElement.classList.contains('dark')) throw new Error('expected dark class');"
        ], html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  Heise.de script regression test
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_HeiseColorSchemeScriptDoesNotThrow()
    {
        var html = "<html><head></head><body></body></html>";
        var script = @"
            var config = JSON.parse(window.localStorage['akwaConfig-v2'] || '{}')
            var scheme = config.colorScheme ? config.colorScheme.scheme : 'auto'
            if (scheme === 'dark' || (scheme === 'auto' && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
              document.documentElement.classList.add('dark')
            }
        ";
        var result = _engine.Execute([script], html);
        Assert.True(result);
    }

    /// <summary>
    /// Regression test for JSException: 'Cannot get property consent of null'.
    /// When <c>localStorage.getItem</c> returns null, <c>JSON.parse(null)</c> yields null
    /// and property access on null throws. The per-script error handling in
    /// <see cref="ScriptEngine"/> must prevent this from crashing the pipeline;
    /// the failing script returns false but does not block subsequent scripts.
    /// </summary>
    [Fact]
    public void Execute_WithHtml_HeiseConsentScriptWithNullGetItem_DoesNotCrash()
    {
        var html = "<html><head></head><body></body></html>";

        // This is the exact pattern from heise.de that caused the crash:
        // getItem returns null → JSON.parse(null) → null → null.consent throws.
        var crashingScript = @"
            var ls = JSON.parse(window.localStorage.getItem('akwaConfig-v2'))
            if (ls.consent && ls.consent[820]) {
              // kameleoon loader
            }
        ";

        // A subsequent script that should still execute despite the crash above.
        var safeScript = "var ok = true;";

        var result = _engine.Execute([crashingScript, safeScript], html);

        // The crashing script makes the overall result false, but the second
        // script must still have executed (no unhandled exception).
        Assert.False(result);
    }

    // ------------------------------------------------------------------
    //  document.createTextNode
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_CreateTextNodeReturnsNode()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["var t = document.createTextNode('Hello'); if (t.textContent !== 'Hello') throw new Error('wrong text');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_CreateTextNodeHasTagName()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["var t = document.createTextNode('test'); if (t.tagName !== '#TEXT') throw new Error('wrong tagName: ' + t.tagName);"],
            html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  appendChild / removeChild / replaceChild
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_AppendChild()
    {
        var html = "<html><body><div id=\"parent\">Parent</div></body></html>";
        var result = _engine.Execute(
        [
            @"var parent = document.getElementById('parent');
              var child = document.createElement('span');
              parent.appendChild(child);
              if (parent.childNodes.length < 1) throw new Error('expected child');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_RemoveChild()
    {
        var html = "<html><body><div id=\"parent\">Parent</div></body></html>";
        var result = _engine.Execute(
        [
            @"var parent = document.getElementById('parent');
              var child = document.createElement('span');
              parent.appendChild(child);
              parent.removeChild(child);
              if (parent.childNodes.length !== 0) throw new Error('expected empty children');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_ReplaceChild()
    {
        var html = "<html><body><div id=\"parent\">Parent</div></body></html>";
        var result = _engine.Execute(
        [
            @"var parent = document.getElementById('parent');
              var old = document.createElement('span');
              parent.appendChild(old);
              var replacement = document.createElement('div');
              parent.replaceChild(replacement, old);
              if (parent.childNodes.length !== 1) throw new Error('expected one child');
              if (parent.firstChild.tagName !== 'DIV') throw new Error('expected DIV');"
        ], html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  parentNode / childNodes / firstChild / lastChild / nextSibling
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_ParentNode()
    {
        var html = "<html><body><div id=\"parent\">Parent</div></body></html>";
        var result = _engine.Execute(
        [
            @"var parent = document.getElementById('parent');
              var child = document.createElement('span');
              parent.appendChild(child);
              if (child.parentNode === null) throw new Error('parentNode should not be null');
              if (child.parentNode.tagName !== 'DIV') throw new Error('parentNode tagName should be DIV');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_FirstChildAndLastChild()
    {
        var html = "<html><body><div id=\"parent\">Parent</div></body></html>";
        var result = _engine.Execute(
        [
            @"var parent = document.getElementById('parent');
              var first = document.createElement('span');
              var last = document.createElement('p');
              parent.appendChild(first);
              parent.appendChild(last);
              if (parent.firstChild.tagName !== 'SPAN') throw new Error('firstChild should be SPAN');
              if (parent.lastChild.tagName !== 'P') throw new Error('lastChild should be P');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_NextSibling()
    {
        var html = "<html><body><div id=\"parent\">Parent</div></body></html>";
        var result = _engine.Execute(
        [
            @"var parent = document.getElementById('parent');
              var a = document.createElement('span');
              var b = document.createElement('p');
              parent.appendChild(a);
              parent.appendChild(b);
              if (a.nextSibling === null) throw new Error('nextSibling should not be null');
              if (a.nextSibling.tagName !== 'P') throw new Error('nextSibling should be P');
              if (b.nextSibling !== null) throw new Error('last child nextSibling should be null');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_ChildNodesEmpty()
    {
        var html = "<html><body><div id=\"el\">Text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              if (el.childNodes.length !== 0) throw new Error('expected empty childNodes');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_ParentNodeNullForOrphan()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.createElement('span');
              if (el.parentNode !== null) throw new Error('orphan parentNode should be null');"
        ], html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  addEventListener / removeEventListener
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_AddEventListener()
    {
        var html = "<html><body><div id=\"el\">Text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              var clicked = false;
              el.addEventListener('click', function() { clicked = true; });
              // Listener is registered (no error thrown)"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_RemoveEventListener()
    {
        var html = "<html><body><div id=\"el\">Text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              var handler = function() {};
              el.addEventListener('click', handler);
              el.removeEventListener('click', handler);
              // No error thrown"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void DomBridge_AddEventListenerStoresListeners()
    {
        var bridge = new DomBridge();
        using var context = new YantraJS.Core.JSContext();
        bridge.Attach(context, "<html><body><div id=\"btn\">Click me</div></body></html>");

        context.Eval(@"
            var el = document.getElementById('btn');
            el.addEventListener('click', function() {});
            el.addEventListener('submit', function() {});
        ");

        var el = bridge.Elements.First(e => e.Id == "btn");
        Assert.True(el.EventListeners.ContainsKey("click"));
        Assert.Single(el.EventListeners["click"]);
        Assert.True(el.EventListeners.ContainsKey("submit"));
    }

    // ------------------------------------------------------------------
    //  window.location
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_WindowLocationIsDefined()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["if (typeof window.location === 'undefined') throw new Error('location is undefined');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void DomBridge_LocationParsesUrl()
    {
        var bridge = new DomBridge();
        using var context = new YantraJS.Core.JSContext();
        bridge.Attach(context, "<html><body></body></html>", "https://www.example.com/path?q=1#hash");

        var href = context.Eval("window.location.href");
        Assert.Contains("example.com", href.ToString());

        var protocol = context.Eval("window.location.protocol");
        Assert.Equal("https:", protocol.ToString());

        var host = context.Eval("window.location.host");
        Assert.Equal("www.example.com", host.ToString());

        var pathname = context.Eval("window.location.pathname");
        Assert.Equal("/path", pathname.ToString());

        var search = context.Eval("window.location.search");
        Assert.Equal("?q=1", search.ToString());

        var hash = context.Eval("window.location.hash");
        Assert.Equal("#hash", hash.ToString());
    }

    // ------------------------------------------------------------------
    //  window.setTimeout / window.setInterval
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_SetTimeoutCallsCallback()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
        [
            @"var called = false;
              window.setTimeout(function() { called = true; }, 0);
              if (!called) throw new Error('setTimeout callback was not invoked');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_SetTimeoutReturnsId()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
        [
            @"var id = window.setTimeout(function() {}, 100);
              if (typeof id !== 'number') throw new Error('expected number id');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_SetIntervalCallsCallback()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
        [
            @"var called = false;
              window.setInterval(function() { called = true; }, 0);
              if (!called) throw new Error('setInterval callback was not invoked');"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_ClearTimeoutDoesNotThrow()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["window.clearTimeout(1);"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_ClearIntervalDoesNotThrow()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["window.clearInterval(1);"],
            html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  window.alert / console.log
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_AlertDoesNotThrow()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["window.alert('test message');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_ConsoleLogDoesNotThrow()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["console.log('hello', 'world');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_WindowConsoleLogDoesNotThrow()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["window.console.log('test');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_ConsoleWarnAndErrorDoNotThrow()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
        [
            "console.warn('warning'); console.error('error'); console.info('info');"
        ], html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  CSS specificity
    // ------------------------------------------------------------------

    [Fact]
    public void CalculateSpecificity_TagSelector_Returns1() => Assert.Equal(1, DomBridge.CalculateSpecificity("p"));

    [Fact]
    public void CalculateSpecificity_ClassSelector_Returns10() => Assert.Equal(10, DomBridge.CalculateSpecificity(".box"));

    [Fact]
    public void CalculateSpecificity_IdSelector_Returns100() => Assert.Equal(100, DomBridge.CalculateSpecificity("#main"));

    [Fact]
    public void CalculateSpecificity_CompoundSelector() =>
        // div.box#main = 1 (tag) + 10 (class) + 100 (id) = 111
        Assert.Equal(111, DomBridge.CalculateSpecificity("div.box#main"));

    [Fact]
    public void CalculateSpecificity_MultipleClasses() =>
        // .a.b = 10 + 10 = 20
        Assert.Equal(20, DomBridge.CalculateSpecificity(".a.b"));

    // ------------------------------------------------------------------
    //  <style> tag extraction and cascading
    // ------------------------------------------------------------------

    [Fact]
    public void DomBridge_ExtractsStyleBlocks()
    {
        var bridge = new DomBridge();
        using var context = new YantraJS.Core.JSContext();
        bridge.Attach(context, "<html><head><style>.red { color: red; }</style></head><body><div class=\"red\">Hello</div></body></html>");

        Assert.True(bridge.CssRules.Count > 0);
        Assert.Equal(".red", bridge.CssRules[0].Selector);
    }

    [Fact]
    public void DomBridge_AppliesCascadedStylesToElement()
    {
        var bridge = new DomBridge();
        using var context = new YantraJS.Core.JSContext();
        bridge.Attach(context,
            "<html><head><style>.box { background-color: blue; font-size: 16px; }</style></head>" +
            "<body><div class=\"box\" id=\"el\">Hello</div></body></html>");

        var el = bridge.Elements.First(e => e.Id == "el");
        Assert.Equal("blue", el.Style["background-color"]);
        Assert.Equal("16px", el.Style["font-size"]);
    }

    [Fact]
    public void DomBridge_InlineStyleOverridesCascaded()
    {
        var bridge = new DomBridge();
        using var context = new YantraJS.Core.JSContext();
        bridge.Attach(context,
            "<html><head><style>.box { color: blue; }</style></head>" +
            "<body><div class=\"box\" id=\"el\" style=\"color: red\">Hello</div></body></html>");

        var el = bridge.Elements.First(e => e.Id == "el");
        // Inline style should win
        Assert.Equal("red", el.Style["color"]);
    }

    [Fact]
    public void DomBridge_MediaScreenRulesAreIncluded()
    {
        var bridge = new DomBridge();
        using var context = new YantraJS.Core.JSContext();
        bridge.Attach(context,
            "<html><head><style>@media screen { .vis { display: block; } }</style></head>" +
            "<body><div class=\"vis\" id=\"el\">Hello</div></body></html>");

        var el = bridge.Elements.First(e => e.Id == "el");
        Assert.Equal("block", el.Style["display"]);
    }

    // ------------------------------------------------------------------
    //  textContent property
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_TextContentReadable()
    {
        var html = "<html><body><div id=\"el\">Some text</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              var tc = el.textContent;
              if (tc !== 'Some text') throw new Error('expected Some text, got ' + tc);"
        ], html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_TextContentWritable()
    {
        var html = "<html><body><div id=\"el\">Old</div></body></html>";
        var result = _engine.Execute(
        [
            @"var el = document.getElementById('el');
              el.textContent = 'New';
              if (el.textContent !== 'New') throw new Error('expected New');"
        ], html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  fetch polyfill (basic structure test)
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_FetchIsDefined()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["if (typeof fetch !== 'function') throw new Error('fetch is not defined');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_WindowFetchIsDefined()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["if (typeof window.fetch !== 'function') throw new Error('window.fetch is not defined');"],
            html);
        Assert.True(result);
    }

    // ------------------------------------------------------------------
    //  XMLHttpRequest polyfill
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_WithHtml_XMLHttpRequestIsDefined()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            ["if (typeof XMLHttpRequest !== 'function') throw new Error('XMLHttpRequest not defined');"],
            html);
        Assert.True(result);
    }

    [Fact]
    public void Execute_WithHtml_XMLHttpRequestCanBeCreated()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
        [
            @"var xhr = new XMLHttpRequest();
              if (xhr.readyState !== 0) throw new Error('expected readyState 0');
              xhr.open('GET', 'http://localhost');
              if (xhr.readyState !== 1) throw new Error('expected readyState 1');"
        ], html);
        Assert.True(result);
    }
}
