namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 5 Acid3 compliance: HTML DOM interfaces —
/// HTMLTableElement, HTMLFormElement, HTMLSelectElement, HTMLOptionElement,
/// HTMLButtonElement, className/class attribute sync, DOM attribute reflection
/// (htmlFor, httpEquiv), area element attributes, and object.data URI resolution.
/// </summary>
public class HtmlDomTests
{
    // ────────────────────── Test 49: Basic table accessors ──────────────────────

    [Fact]
    public void Table_CreateCaption_Creates_And_Returns_Caption()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
r.push(!table.caption);  // initially null
var caption = table.createCaption();
r.push(table.caption === caption);  // returns created element
r.push(table.childNodes.length === 1);  // one child added
table.caption = caption;  // no-op
r.push(table.caption === caption);  // still the same
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void Table_CreateTHead_CreateTFoot_Work()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
r.push(!table.tHead);
r.push(!table.tFoot);
var thead = table.createTHead();
var tfoot = table.createTFoot();
r.push(table.tHead === thead);
r.push(table.tFoot === tfoot);
r.push(table.childNodes.length === 2);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void Table_TBodies_Returns_Empty_Collection()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
r.push(!!table.tBodies);  // truthy
r.push(table.tBodies.length === 0);
table.createCaption();
table.createTHead();
table.createTFoot();
r.push(table.tBodies.length === 0);  // still no tbody
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Table_Rows_Returns_Empty_Initially()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
r.push(!!table.rows);  // truthy
r.push(table.rows.length === 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Table_DeleteCaption_DeleteTHead_DeleteTFoot_Work()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
table.createCaption();
table.createTHead();
table.createTFoot();
r.push(table.childNodes.length === 3);
table.deleteCaption();
table.deleteTHead();
table.deleteTFoot();
r.push(!table.caption);
r.push(!table.tHead);
r.push(!table.tFoot);
r.push(!table.hasChildNodes());
r.push(table.childNodes.length === 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    // ────────────────────── Test 50: Table construction ──────────────────────

    [Fact]
    public void Table_TBodies_InsertRow_Creates_Row_In_Section()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
table.appendChild(document.createElement('tbody'));
var tr1 = document.createElement('tr');
table.appendChild(tr1);
table.appendChild(document.createElement('caption'));
table.appendChild(document.createElement('thead'));
// <table><tbody/><tr/><caption/><thead/>
table.insertBefore(table.firstChild.nextSibling, null); // move the <tr/> to the end
// <table><tbody/><caption/><thead/><tr/>
table.replaceChild(table.firstChild, table.lastChild); // move the <tbody/> to the end and remove the <tr>
// <table><caption/><thead/><tbody/>
var tr2 = table.tBodies[0].insertRow(0);
r.push(table.tBodies[0].rows[0].rowIndex === 0);
r.push(table.tBodies[0].rows[0].sectionRowIndex === 0);
r.push(table.childNodes.length === 3);
r.push(!!table.caption);
r.push(!!table.tHead);
r.push(!table.tFoot);
r.push(table.tBodies.length === 1);
r.push(table.rows.length === 1);
r.push(!tr1.parentNode);  // orphan row
r.push(table.caption === table.createCaption());
r.push(table.tFoot === null);
r.push(table.tHead === table.createTHead());
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void Table_Rows_Order_THead_TBodies_TFoot()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
table.appendChild(document.createElement('tbody'));
var tr1 = document.createElement('tr');
table.appendChild(tr1);
table.appendChild(document.createElement('caption'));
table.appendChild(document.createElement('thead'));
// <table><tbody/><tr/><caption/><thead/>
table.insertBefore(table.firstChild.nextSibling, null);
// <table><tbody/><caption/><thead/><tr/>
table.replaceChild(table.firstChild, table.lastChild);
// <table><caption/><thead/><tbody/>

var tr2 = table.tBodies[0].insertRow(0);
table.createTFoot();
tr1 = document.createElement('tr');
table.tHead.appendChild(tr1);
r.push(table.rows[0] === table.tHead.firstChild);  // thead row first
r.push(table.rows.length === 2);
r.push(table.rows[1] === table.tBodies[0].firstChild);  // tbody row second
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────────────── Test 51: Row ordering ──────────────────────

    [Fact]
    public void Table_InsertRow_Complex_Ordering()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
var rows = [
  document.createElement('tr'),    // 0
  document.createElement('tr'),    // 1
  document.createElement('tr'),    // 2
  document.createElement('tr'),    // 3
  document.createElement('tr'),    // 4
  table.insertRow(0),              // 5
  table.createTFoot().insertRow(0) // 6
];
rows[6].parentNode.appendChild(rows[0]);
table.appendChild(rows[1]);
table.insertBefore(document.createElement('thead'), table.firstChild);
table.firstChild.appendChild(rows[2]);
rows[2].parentNode.appendChild(rows[3]);
rows[4].appendChild(rows[5].parentNode);
table.insertRow(0);
table.tFoot.appendChild(rows[6]);
r.push(table.rows.length === 6);
r.push(table.getElementsByTagName('tr').length === 6);
r.push(table.childNodes.length === 3);
r.push(table.childNodes[0] === table.tHead);
r.push(table.childNodes[1] === table.tFoot);
r.push(table.tBodies.length === 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    // ────────────────────── Test 52: form.elements ──────────────────────

    [Fact]
    public void Form_Elements_Returns_Controls_Collection()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form action="""" name=""form""><input type=HIDDEN></form>
<div id=""result""></div>
<script>
var r = [];
var form = document.getElementsByTagName('form')[0];
r.push(form.elements !== form);
r.push(form.elements.length === 1);
r.push(form.elements.length === form.length);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────────────── Test 53: Dynamic input changes ──────────────────────

    [Fact]
    public void Input_Name_ReadWrite_Syncs_With_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var f = document.createElement('form');
var i = document.createElement('input');
i.name = 'first';
i.type = 'text';
i.value = 'test';
f.appendChild(i);
r.push(i.getAttribute('name') === 'first');
r.push(i.name === 'first');
r.push(i.getAttribute('type') === 'text');
r.push(i.type === 'text');
r.push(!i.hasAttribute('value'));  // value IDL property not reflected
r.push(i.value === 'test');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    [Fact]
    public void Form_Elements_By_Name_Access()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var f = document.createElement('form');
var i = document.createElement('input');
i.name = 'first';
i.type = 'text';
i.value = 'test';
f.appendChild(i);
r.push(f.elements.length === 1);
r.push(f.elements[0] === i);
r.push(f.elements.first === i);
r.push(f.elements.second === null);
i.name = 'second';
r.push(f.elements.second === i);
r.push(f.elements.first === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    // ────────────────────── Test 54: Parsed input changes ──────────────────────

    [Fact]
    public void Input_Type_Returns_Lowercase()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form action="""" name=""form""><input type=HIDDEN></form>
<div id=""result""></div>
<script>
var r = [];
var i = document.getElementsByTagName('input')[0];
r.push(i.getAttribute('type') === 'HIDDEN');
r.push(i.type === 'hidden');
i.name = 'test';
r.push(i.parentNode.elements.test === i);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Input_Click_Dispatches_Submit_Event()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form action="""" name=""form""><input type=HIDDEN></form>
<div id=""result""></div>
<script>
var r = [];
var i = document.getElementsByTagName('input')[0];
i.parentNode.action = 'javascript:';
var called = false;
i.parentNode.onsubmit = function(arg) {
  arg.preventDefault();
  called = true;
};
i.type = 'submit';
i.click();
r.push(called);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void Input_SetAttribute_Returns_String()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form action="""" name=""form""><input type=HIDDEN></form>
<div id=""result""></div>
<script>
var r = [];
var i = document.getElementsByTagName('input')[0];
i.setAttribute('maxLength', '2');
var s = i.getAttribute('maxLength');
r.push(!!s.match);  // is a String (has match method)
r.push(!s.MIN_VALUE);  // is not a Number
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────────── Test 55: Moved checkboxes keep state ──────────────────────

    [Fact]
    public void Checkbox_State_Persists_After_Move()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe src=""empty.html""></iframe><iframe src=""empty.html""></iframe>
<div id=""result""></div>
<script>
var r = [];
var container = document.getElementsByTagName('iframe')[0];
var input1 = document.createElement('input');
container.appendChild(input1);
input1.type = 'checkbox';
input1.checked = true;
r.push(input1.checked);  // checked after setting

var input2 = document.createElement('input');
input2.type = 'checkbox';
container.appendChild(input2);
input2.checked = true;
r.push(input2.checked);  // checked after setting

var target = document.getElementsByTagName('iframe')[1];
target.appendChild(input1);
target.appendChild(input2);
r.push(input1.checked);  // still checked after move
r.push(input2.checked);  // still checked after move
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ────────────────────── Test 56: Cloned radio buttons ──────────────────────

    [Fact]
    public void Radio_Button_Mutual_Exclusion_In_Form()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form action="""" name=""form""><input type=HIDDEN></form>
<div id=""result""></div>
<script>
var r = [];
var form = document.getElementsByTagName('form')[0];
var input1 = document.createElement('input');
input1.type = 'radio';
input1.name = 'radioGroup1';
form.appendChild(input1);
var input2 = input1.cloneNode(true);
input1.parentNode.appendChild(input2);
input1.checked = true;
r.push(!!form.elements.radioGroup1);  // radio group accessible
r.push(input1.checked);
r.push(!input2.checked);
input2.checked = true;
r.push(!input1.checked);  // first unchecked
r.push(input2.checked);   // second checked
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void Radio_Button_Different_Groups_Independent()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form action="""" name=""form""><input type=HIDDEN></form>
<div id=""result""></div>
<script>
var r = [];
var form = document.getElementsByTagName('form')[0];
var input1 = document.createElement('input');
input1.type = 'radio';
input1.name = 'radioGroup1';
form.appendChild(input1);
var input2 = input1.cloneNode(true);
form.appendChild(input2);
input1.checked = true;

var input3 = document.createElement('input');
input3.type = 'radio';
input3.name = 'radioGroup2';
form.appendChild(input3);
input3.checked = true;
r.push(input1.checked);   // still checked (different group)
r.push(input3.checked);   // checked
input1.checked = true;
r.push(!input2.checked);  // unchecked by mutual exclusion
r.push(input3.checked);   // still checked (different group)
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ────────────────────── Test 57: HTMLSelectElement.add() ──────────────────────

    [Fact]
    public void Select_Add_Appends_Option()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var s = document.createElement('select');
var o = document.createElement('option');
s.add(o, null);
r.push(s.firstChild === o);
r.push(s.childNodes.length === 1);
r.push(s.childNodes[0] === o);
r.push(s.options.length === 1);
r.push(s.options[0] === o);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    // ────────────────────── Test 58: HTMLOptionElement.defaultSelected ──────────────────────

    [Fact]
    public void Option_DefaultSelected_Sets_SelectedIndex()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var s = document.createElement('select');
var o1 = document.createElement('option');
var o2 = document.createElement('option');
o2.defaultSelected = true;
var o3 = document.createElement('option');
s.appendChild(o1);
s.appendChild(o2);
s.appendChild(o3);
r.push(s.options[s.selectedIndex] === o2);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    // ────────────────────── Test 59: Button element attributes ──────────────────────

    [Fact]
    public void Button_Type_Default_Submit()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var button = document.createElement('button');
r.push(button.type === 'submit');
button.setAttribute('type', 'button');
r.push(button.type === 'button');
button.removeAttribute('type');
r.push(button.type === 'submit');  // back to default
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Button_Value_Reflects_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var button = document.createElement('button');
button.setAttribute('value', 'apple');
button.appendChild(document.createTextNode('banana'));
r.push(button.value === 'apple');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    // ────────────────────── Test 60: className vs class ──────────────────────

    [Fact]
    public void ClassName_SetAttribute_Class_Syncs()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div class=""buckets""></div>
<div id=""result""></div>
<script>
var r = [];
var span = document.createElement('span');
span.setAttribute('class', 'kittens');
span.className = 'cats';
r.push(span.getAttribute('class') === 'cats');
r.push(span.className === 'cats');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────────── Test 61: className space preservation ──────────────────────

    [Fact]
    public void ClassName_Preserves_Whitespace()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var p = document.createElement('p');
r.push(!p.hasAttribute('class'));  // no class initially
p.setAttribute('class', ' te  st ');
r.push(p.hasAttribute('class'));
r.push(p.getAttribute('class') === ' te  st ');
r.push(p.className === ' te  st ');
p.className = p.className.replace(/ /g, '\n');
r.push(p.hasAttribute('class'));
r.push(p.getAttribute('class') === '\nte\n\nst\n');
r.push(p.className === '\nte\n\nst\n');
p.className = '';
r.push(p.hasAttribute('class'));  // class attribute still exists
r.push(p.getAttribute('class') === '');
r.push(p.className === '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true,true,true,true", result);
    }

    // ────────────────────── Test 62: DOM attributes vs content attributes ──────────────────────

    [Fact]
    public void ClassName_Not_Same_As_Class_Property()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div class=""buckets""></div>
<div id=""result""></div>
<script>
var r = [];
var test = document.getElementsByTagName('div')[0];
r.push(test.className === 'buckets');
r.push(test.getAttribute('class') === 'buckets');
r.push(!test.hasAttribute('className'));
r.push(!('class' in test));
test['class'] = 'oil';
r.push(test.className !== 'oil');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void Label_HtmlFor_Maps_To_For_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var label = document.createElement('label');
label.htmlFor = 'jars';
r.push(label.htmlFor === 'jars');
r.push(label.getAttribute('for') === 'jars');
r.push(!label.hasAttribute('htmlFor'));
r.push(!('for' in label));
label = document.createElement('label');
label.setAttribute('for', 'pots');
r.push(label.htmlFor === 'pots');
r.push(label.getAttribute('for') === 'pots');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    [Fact]
    public void Meta_HttpEquiv_Maps_To_Http_Equiv_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var meta = document.createElement('meta');
meta.setAttribute('http-equiv', 'boxes');
r.push(meta.httpEquiv === 'boxes');
r.push(meta.getAttribute('http-equiv') === 'boxes');
r.push(!meta.hasAttribute('httpEquiv'));
meta = document.createElement('meta');
meta.httpEquiv = 'cans';
r.push(meta.httpEquiv === 'cans');
r.push(meta.getAttribute('http-equiv') === 'cans');
r.push(!meta.hasAttribute('httpEquiv'));
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    // ────────────────────── Test 63: Area element attributes ──────────────────────

    [Fact]
    public void Area_GetAttribute_Returns_Correct_Values()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>document.write('<map name=""""><area href="""" shape=""rect"" coords=""2,2,4,4"" alt=""<\'>""><\/map>');</script>
<div id=""result""></div>
<script>
var r = [];
var area = document.getElementsByTagName('area')[0];
r.push(area.getAttribute('href') === '');
r.push(area.getAttribute('shape') === 'rect');
r.push(area.getAttribute('coords') === '2,2,4,4');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────────────── Test 64: Object.data URI resolution ──────────────────────

    [Fact]
    public void Object_Data_Resolves_Relative_URIs()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var obj1 = document.createElement('object');
obj1.setAttribute('data', 'test.html');
var obj2 = document.createElement('object');
obj2.setAttribute('data', './test.html');
r.push(obj1.data === obj2.data);  // both resolve same
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "http://example.com/dir/page.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void Object_Data_Is_Absolute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var obj1 = document.createElement('object');
obj1.setAttribute('data', 'test.html');
r.push(!!obj1.data.match(/^http:/));  // should be absolute
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "http://example.com/dir/page.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void Nonexistent_Property_Returns_Undefined()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var test = document.createElement('p');
r.push(test.TWVvdywgbWV3Li4u === undefined);
test.setAttribute('TWVvdywgbWV3Li4u', 'woof');
r.push(test.TWVvdywgbWV3Li4u === undefined);  // still undefined as property
r.push(test.getAttribute('TWVvdywgbWV3Li4u') === 'woof');  // but getAttribute works
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    // ────────────────────── Additional edge case tests ──────────────────────

    [Fact]
    public void Table_CreateCaption_Returns_Existing()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
var c1 = table.createCaption();
var c2 = table.createCaption();
r.push(c1 === c2);  // should return the same element
r.push(table.childNodes.length === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Form_Elements_Length_Dynamic()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var f = document.createElement('form');
r.push(f.elements.length === 0);
var i = document.createElement('input');
f.appendChild(i);
r.push(f.elements.length === 1);
var s = document.createElement('select');
f.appendChild(s);
r.push(f.elements.length === 2);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Input_Type_Change_And_Click_Submit()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var f = document.createElement('form');
var i = document.createElement('input');
f.appendChild(i);
i.type = 'hIdDeN';
r.push(i.type === 'hidden');  // type getter lowercases
r.push(i.getAttribute('type') === 'hIdDeN');  // attribute preserves case
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Anchor_Href_Property_ReadWrite()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var a = document.createElement('a');
a.setAttribute('href', 'http://www.example.com/');
a.appendChild(document.createTextNode('www.example.com'));
a.href = 'http://hixie.ch/';
r.push(a.firstChild.data === 'www.example.com');  // text not changed by href set
a.href = 'http://damowmow.com/';
r.push(a.firstChild.data === 'www.example.com');  // still not changed
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }
}
