using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Form-associated custom elements (HTML §4.13.5): <c>static formAssociated</c>,
/// <c>attachInternals()</c>, the <c>ElementInternals</c> it hands back, and the reactions a
/// definition receives about its form.
/// </summary>
/// <remarks>
/// <para>
/// This is the last of the three capabilities the Custom Elements slice named and left out.
/// <c>attachInternals()</c> was undefined, so the line every such component's constructor opens with
/// — <c>this.internals_ = this.attachInternals()</c> — was a <c>TypeError</c> that took the
/// constructor down, and with it the upgrade of every instance on the page.
/// </para>
/// <para>
/// <b>Nothing here is a shape-only stub.</b> <c>setFormValue</c>'s value is read back where a browser
/// reads it — constructing the form's entry list, which is what <c>new FormData(form)</c> hands over
/// — and <c>setValidity</c>'s flags are what the owning form's <c>checkValidity</c> answers from.
/// </para>
/// <para>Every expectation is Chromium's measured answer over the same probe run against both.</para>
/// </remarks>
public sealed class FormAssociatedCustomElementTests
{
    private const string Markup =
        "<!DOCTYPE html><html><body>" +
        "<form id=\"f1\"><label for=\"a\">L</label><fieldset id=\"fs\"><my-in id=\"a\" name=\"an\"></my-in></fieldset>" +
        "<my-in id=\"b\"></my-in><input name=\"plain\" value=\"pv\"></form>" +
        "<form id=\"f2\"></form><my-in id=\"loose\"></my-in>" +
        "</body></html>";

    /// <summary>The component every test defines: form-associated, with its internals kept where the
    /// test can reach them and every reaction logged.</summary>
    private const string Definition = """
        var log = [];
        class MyIn extends HTMLElement {
            static formAssociated = true;
            constructor() { super(); this.i = this.attachInternals(); }
            formAssociatedCallback(f) { log.push('assoc ' + this.id + ':' + (f ? f.id : String(f))); }
            formDisabledCallback(d) { log.push('disabled ' + this.id + ':' + d); }
            formResetCallback() { log.push('reset ' + this.id); }
        }
        customElements.define('my-in', MyIn);
        var a = document.getElementById('a'), b = document.getElementById('b'),
            loose = document.getElementById('loose');
        """;

    private static DomBridge Attach(out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, Markup, "https://example.com/index.html");
        return bridge;
    }

    private static string Eval(JSContext context, string body) =>
        context.Eval($$"""
            (() => {
                {{Definition}}
                {{body}}
            })()
            """).ToString();

    /// <summary>
    /// <c>attachInternals()</c> hands back a real <c>ElementInternals</c> whose members are on its
    /// prototype — an instance has no own properties, the shape <c>Range</c>, <c>Selection</c> and
    /// <c>Blob</c> established and the one Chromium reports.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AttachInternals_Returns_An_ElementInternals_With_No_Own_Properties()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("ElementInternals/0/function/TypeError", Eval(context, """
            var bare;
            try { new ElementInternals(); bare = 'ok'; } catch (e) { bare = e.name; }
            return a.i.constructor.name + '/' + Object.getOwnPropertyNames(a.i).length + '/' +
                   (typeof ElementInternals) + '/' + bare;
            """));
    }

    /// <summary>
    /// <c>attachInternals</c> refuses twice on the same element and refuses entirely for an element
    /// that is not a custom element. It is installed on every element rather than only the custom
    /// ones because that is where a browser puts it — being absent would make the standard
    /// feature-detect answer the wrong way.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AttachInternals_Refuses_Twice_And_Refuses_A_Plain_Element()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("function/NotSupportedError/NotSupportedError", Eval(context, """
            var twice, plain;
            try { a.attachInternals(); twice = 'ok'; } catch (e) { twice = e.name; }
            try { document.createElement('div').attachInternals(); plain = 'ok'; } catch (e) { plain = e.name; }
            return (typeof document.createElement('div').attachInternals) + '/' + twice + '/' + plain;
            """));
    }

    /// <summary>
    /// Every form-related member refuses on an element whose definition did not declare
    /// <c>formAssociated</c>, rather than answering an empty or neutral value.
    /// </summary>
    /// <remarks>
    /// The distinction is observable and specified: answering <c>null</c> for <c>form</c> would say
    /// "this control has no form" where the truth is "this is not a control". <c>states</c> and
    /// <c>shadowRoot</c> are the two that work regardless, because they are not about forms — and a
    /// <c>formAssociated</c> written as an instance getter rather than a static does not count,
    /// which is measured rather than assumed.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void The_Form_Members_Refuse_On_A_Non_Form_Associated_Element()
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "form=NotSupportedError,labels=NotSupportedError,willValidate=NotSupportedError," +
            "validity=NotSupportedError,validationMessage=NotSupportedError," +
            "setFormValue=NotSupportedError,setValidity=NotSupportedError," +
            "checkValidity=NotSupportedError,reportValidity=NotSupportedError/" +
            "CustomStateSet/null/NotSupportedError",
            Eval(context, """
                class Plain extends HTMLElement { constructor() { super(); this.p = this.attachInternals(); } }
                customElements.define('plain-el', Plain);
                var p = document.createElement('plain-el').p;
                var r = [];
                [['form', () => p.form], ['labels', () => p.labels], ['willValidate', () => p.willValidate],
                 ['validity', () => p.validity], ['validationMessage', () => p.validationMessage],
                 ['setFormValue', () => p.setFormValue('x')], ['setValidity', () => p.setValidity({})],
                 ['checkValidity', () => p.checkValidity()], ['reportValidity', () => p.reportValidity()]
                ].forEach(function (probe) {
                    try { probe[1](); r.push(probe[0] + '=ok'); }
                    catch (e) { r.push(probe[0] + '=' + e.name); }
                });
                // An instance getter named formAssociated is not the static the specification reads.
                class Q extends HTMLElement { get formAssociated() { return true; } constructor() { super(); this.q = this.attachInternals(); } }
                customElements.define('q-el', Q);
                var instanceGetter;
                try { document.createElement('q-el').q.form; instanceGetter = 'ok'; }
                catch (e) { instanceGetter = e.name; }
                return r.join(',') + '/' + p.states.constructor.name + '/' + String(p.shadowRoot) + '/' + instanceGetter;
                """));
    }

    /// <summary>
    /// The form-association reads: the owner, the labels pointing at it, and whether it will be
    /// validated. A form-associated custom element is labelable in its own right, which no tag list
    /// can answer for.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Internals_Report_The_Form_The_Labels_And_WillValidate()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("f1/1/NodeList/L/true/null", Eval(context, """
            return a.i.form.id + '/' + a.i.labels.length + '/' + a.i.labels.constructor.name + '/' +
                   a.i.labels[0].textContent + '/' + a.i.willValidate + '/' + String(loose.i.form);
            """));
    }

    /// <summary>
    /// <c>validity</c> is one live <c>ValidityState</c> rather than a snapshot, and it exposes the
    /// specified flags in the specified order.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Validity_Is_A_Live_ValidityState()
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "ValidityState/true/valueMissing,typeMismatch,patternMismatch,tooLong,tooShort," +
            "rangeUnderflow,rangeOverflow,stepMismatch,badInput,customError,valid/true/false/true",
            Eval(context, """
                var validity = a.i.validity;
                var keys = [];
                for (var k in validity) keys.push(k);
                var before = validity.valid;
                a.i.setValidity({ valueMissing: true }, 'please fill');
                return validity.constructor.name + '/' + (validity === a.i.validity) + '/' +
                       keys.join(',') + '/' + before + '/' + validity.valid + '/' + validity.valueMissing;
                """));
    }

    /// <summary>
    /// <c>setValidity</c> makes the element invalid and the owning form invalid with it, and
    /// requires a message whenever it raises a flag.
    /// </summary>
    /// <remarks>
    /// The message requirement is measured: an omitted message with a flag raised is a
    /// <c>TypeError</c> rather than an empty message, which is what stops a component reporting a
    /// failure a user cannot be told about.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void SetValidity_Makes_The_Element_And_Its_Form_Invalid()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("\"please fill\"/false/false/TypeError/true/\"\"", Eval(context, """
            a.i.setValidity({ valueMissing: true }, 'please fill');
            var message = JSON.stringify(a.i.validationMessage);
            var elementValid = a.i.checkValidity();
            var formValid = document.getElementById('f1').checkValidity();
            var noMessage;
            try { a.i.setValidity({ customError: true }); noMessage = 'ok'; } catch (e) { noMessage = e.name; }
            a.i.setValidity({});
            return message + '/' + elementValid + '/' + formValid + '/' + noMessage + '/' +
                   a.i.validity.valid + '/' + JSON.stringify(a.i.validationMessage);
            """));
    }

    /// <summary>An invalid element receives an <c>invalid</c> event from <c>checkValidity</c>, which
    /// is how a page hears about a failed control without polling every one of them.</summary>
    [Fact(Timeout = 600000)]
    public void CheckValidity_Fires_An_Invalid_Event()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("false/1/true/1", Eval(context, """
            var fired = 0;
            a.addEventListener('invalid', function () { fired++; });
            a.i.setValidity({ customError: true }, 'nope');
            var invalid = a.i.checkValidity();
            var afterInvalid = fired;
            a.i.setValidity({});
            var valid = a.i.checkValidity();
            return invalid + '/' + afterInvalid + '/' + valid + '/' + fired;
            """));
    }

    /// <summary>
    /// <c>setFormValue</c> is the element's submission value, read back where a browser reads it:
    /// the form's entry list, which is what <c>new FormData(form)</c> hands over.
    /// </summary>
    /// <remarks>
    /// The three shapes are all measured. A string submits under the element's <c>name</c>; a
    /// <c>FormData</c> contributes its own entries and the element's name is not used at all; and
    /// <c>null</c> means the element submits nothing.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void SetFormValue_Is_Read_Back_By_The_Forms_Entry_List()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("an=av,plain=pv/null/\"1\"/\"2\"/null", Eval(context, """
            function keys(form) { var out = []; new FormData(form).forEach(function (v, k) { out.push(k + '=' + v); }); return out.join(','); }
            var form = document.getElementById('f1');
            // b has no name attribute, so its value contributes nothing — the same rule an ordinary
            // control follows.
            a.i.setFormValue('av');
            b.i.setFormValue('bv');
            var withValue = keys(form);
            a.i.setFormValue(null);
            var cleared = JSON.stringify(new FormData(form).get('an'));
            var entries = new FormData();
            entries.append('x', '1');
            entries.append('y', '2');
            a.i.setFormValue(entries);
            var data = new FormData(form);
            return withValue + '/' + cleared + '/' + JSON.stringify(data.get('x')) + '/' +
                   JSON.stringify(data.get('y')) + '/' + JSON.stringify(data.get('an'));
            """));
    }

    /// <summary><c>form.elements</c> lists a form-associated custom element among its controls.</summary>
    [Fact(Timeout = 600000)]
    public void A_Form_Lists_Its_Custom_Controls()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("3/MY-IN/MY-IN/INPUT", Eval(context, """
            var els = document.getElementById('f1').elements;
            return els.length + '/' + els[0].tagName + '/' + els[1].tagName + '/' + els[2].tagName;
            """));
    }

    /// <summary>
    /// <c>formAssociatedCallback</c> reports the form the element belongs to — at the upgrade that
    /// made it a custom element, and again whenever it moves between forms or out of one.
    /// </summary>
    /// <remarks>
    /// An upgrade outside any form reports nothing rather than reporting <c>null</c>: the reaction is
    /// enqueued only for an element whose form owner is non-null, so <c>#loose</c> is silent until
    /// something moves it. Measured — the plausible reading, that every form-associated element hears
    /// about its owner at upgrade, is wrong.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void FormAssociatedCallback_Reports_Every_Owner_Change()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("assoc a:f1,assoc b:f1|assoc a:null,assoc a:f2,assoc a:null", Eval(context, """
            var upgrade = log.join(',');
            log.length = 0;
            document.getElementById('f2').appendChild(a);
            a.remove();
            return upgrade + '|' + log.join(',');
            """));
    }

    /// <summary>
    /// <c>formDisabledCallback</c> reports the element's disabled state, which an ancestor
    /// <c>&lt;fieldset disabled&gt;</c> changes as much as its own attribute does — and a disabled
    /// element will not be validated.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void FormDisabledCallback_Reports_The_Fieldset_As_Well_As_The_Element()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("disabled a:true/false/disabled a:false/true/disabled b:true", Eval(context, """
            var out = [];
            log.length = 0;
            document.getElementById('fs').setAttribute('disabled', '');
            out.push(log.join(','), String(a.i.willValidate));
            log.length = 0;
            document.getElementById('fs').removeAttribute('disabled');
            out.push(log.join(','), String(a.i.willValidate));
            log.length = 0;
            b.setAttribute('disabled', '');
            out.push(log.join(','));
            return out.join('/');
            """));
    }

    /// <summary>
    /// A form reset reaches its custom controls as <c>formResetCallback</c>. They have no dirty
    /// flags to clear — a custom control's value is whatever it chose to submit — so the reaction is
    /// the whole of what a reset can mean for one.
    /// </summary>
    /// <remarks>
    /// <c>formStateRestoreCallback</c> is deliberately never fired: it reports a value restored by
    /// session history or an autofill pass, and this engine performs neither, so firing it would be
    /// an invention rather than a restoration.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void FormResetCallback_Reaches_The_Custom_Controls()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("reset a,reset b", Eval(context, """
            log.length = 0;
            document.getElementById('f1').reset();
            return log.join(',');
            """));
    }

    /// <summary>
    /// <c>states</c> is a real setlike <c>CustomStateSet</c> — iterable, with the whole set surface —
    /// and it answers for a custom element whether or not it is form-associated.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void States_Is_A_Setlike_CustomStateSet()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("CustomStateSet/true/2/true/--on+--two/--on+--two/1/0/function", Eval(context, """
            var s = a.i.states;
            s.add('--on');
            s.add('--two');
            var seen = [];
            s.forEach(function (v) { seen.push(v); });
            var spread = [...s].join('+');
            var identity = s === a.i.states;
            var size = s.size, has = s.has('--on');
            s.delete('--on');
            var afterDelete = s.size;
            s.clear();
            return s.constructor.name + '/' + identity + '/' + size + '/' + has + '/' +
                   seen.join('+') + '/' + spread + '/' + afterDelete + '/' + s.size + '/' +
                   (typeof CustomStateSet);
            """));
    }

    /// <summary><c>shadowRoot</c> reports the element's own shadow root — the same one
    /// <c>element.shadowRoot</c> does, through the same implementation.</summary>
    [Fact(Timeout = 600000)]
    public void ShadowRoot_Reports_The_Elements_Own_Root()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("null/true", Eval(context, """
            var before = String(a.i.shadowRoot);
            var root = a.attachShadow({ mode: 'open' });
            return before + '/' + (a.i.shadowRoot === root);
            """));
    }
}
