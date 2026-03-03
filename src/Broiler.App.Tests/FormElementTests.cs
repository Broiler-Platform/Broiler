using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class FormElementTests
{
    private readonly ScriptEngine _engine = new();

    [Fact]
    public void Input_ValueProperty_IsReadWrite()
    {
        var html = "<html><body><input id=\"field\" value=\"initial\"></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.getElementById('field');
                if (el.value !== 'initial') throw new Error('wrong initial value');
                el.value = 'updated';
                if (el.value !== 'updated') throw new Error('value not updated');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void Checkbox_CheckedProperty_IsReadWrite()
    {
        var html = "<html><body><input id=\"cb\" type=\"checkbox\"></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.getElementById('cb');
                if (el.checked !== false) throw new Error('should start unchecked');
                el.checked = true;
                if (el.checked !== true) throw new Error('checked not updated');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void Input_TypeProperty_IsReadable()
    {
        var html = "<html><body><input id=\"field\" type=\"text\"></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.getElementById('field');
                if (el.type !== 'text') throw new Error('wrong type: ' + el.type);
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void Input_NameProperty_IsReadable()
    {
        var html = "<html><body><input id=\"field\" name=\"username\"></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.getElementById('field');
                if (el.name !== 'username') throw new Error('wrong name: ' + el.name);
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void Input_DisabledProperty_IsReadWrite()
    {
        var html = "<html><body><input id=\"field\"></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.getElementById('field');
                if (el.disabled !== false) throw new Error('should start enabled');
                el.disabled = true;
                if (el.disabled !== true) throw new Error('disabled not updated');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void Input_RequiredProperty_IsReadWrite()
    {
        var html = "<html><body><input id=\"field\"></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.getElementById('field');
                if (el.required !== false) throw new Error('should start not required');
                el.required = true;
                if (el.required !== true) throw new Error('required not updated');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void CheckValidity_EmptyRequiredInput_ReturnsFalse()
    {
        var html = "<html><body><input id=\"field\" required></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.getElementById('field');
                if (el.checkValidity() !== false) throw new Error('empty required input should be invalid');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void CheckValidity_FilledRequiredInput_ReturnsTrue()
    {
        var html = "<html><body><input id=\"field\" required value=\"filled\"></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.getElementById('field');
                if (el.checkValidity() !== true) throw new Error('filled required input should be valid');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void FormSubmit_FiresSubmitEvent()
    {
        var html = "<html><body><form id=\"myform\"><input name=\"x\" value=\"1\"></form></body></html>";
        var result = _engine.Execute(
            [ @"
                var submitted = false;
                var form = document.getElementById('myform');
                form.addEventListener('submit', function(e) { submitted = true; });
                form.submit();
                if (!submitted) throw new Error('submit event not fired');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void FormCheckValidity_ValidatesFormElement()
    {
        var html = "<html><body><form id=\"myform\"><input required value=\"ok\"></form></body></html>";
        var result = _engine.Execute(
            [ @"
                var form = document.getElementById('myform');
                if (form.checkValidity() !== true) throw new Error('form should be valid');
            " ], html);
        Assert.True(result);
    }
}
