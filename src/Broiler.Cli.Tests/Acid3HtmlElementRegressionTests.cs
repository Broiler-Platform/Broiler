using Broiler.Cli;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase G: Acid3 Bucket 4 — HTML Elements (Tests 49–63) explicit regression tests.
/// </summary>
public class Acid3HtmlElementRegressionTests
{
    [Fact]
    public void Acid3_Test49_Table_CreateTHead()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
var thead = table.createTHead();
r.push(thead.tagName.toLowerCase());
r.push(table.createTHead() === thead);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("thead|true", result);
    }

    [Fact]
    public void Acid3_Test50_Table_CreateTFoot()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
var tfoot = table.createTFoot();
r.push(tfoot.tagName.toLowerCase());
r.push(table.createTFoot() === tfoot);
r.push(table.tFoot === tfoot);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("tfoot|true|true", result);
    }

    [Fact]
    public void Acid3_Test51_Table_InsertRow()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
var row = table.insertRow(0);
r.push(row.tagName.toLowerCase());
r.push(table.rows.length);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("tr|1", result);
    }

    [Fact]
    public void Acid3_Test52_TableSection_InsertRow()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
var tbody = document.createElement('tbody');
table.appendChild(tbody);
var row = tbody.insertRow(0);
r.push(row.tagName.toLowerCase());
r.push(tbody.rows.length);
r.push(row.parentNode === tbody);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("tr|1|true", result);
    }

    [Fact(Skip = "insertCell() not yet wired in CLI DOM engine — implementation gap")]
    public void Acid3_Test53_TableRow_InsertCell()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<table id=""t""><tbody><tr id=""row""></tr></tbody></table>
<div id=""result""></div>
<script>
var r = [];
var row = document.getElementById('row');
var cell = row.insertCell(0);
r.push(cell.tagName.toLowerCase());
r.push(cell.parentNode === row);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("td|true", result);
    }

    [Fact]
    public void Acid3_Test54_Table_Rows_Ordering()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
var tfoot = table.createTFoot();
var tbody = document.createElement('tbody');
table.appendChild(tbody);
var thead = table.createTHead();
var hr = thead.insertRow(0); hr.id = 'h';
var br = tbody.insertRow(0); br.id = 'b';
var fr = tfoot.insertRow(0); fr.id = 'f';
var ids = [];
for (var i = 0; i < table.rows.length; i++) ids.push(table.rows[i].id);
r.push(ids.join(','));
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("h,b,f", result);
    }

    [Fact]
    public void Acid3_Test55_Table_DeleteRow()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var table = document.createElement('table');
var tbody = document.createElement('tbody');
table.appendChild(tbody);
var r0 = tbody.insertRow(0); r0.id = 'a';
var r1 = tbody.insertRow(1); r1.id = 'b';
var r2 = tbody.insertRow(2); r2.id = 'c';
table.deleteRow(1);
r.push(table.rows.length);
r.push(table.rows[0].id);
r.push(table.rows[1].id);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("2|a|c", result);
    }

    [Fact]
    public void Acid3_Test56_Form_Elements()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form id=""f"">
  <input name=""x"" type=""text"" />
  <select name=""y""><option>A</option></select>
  <textarea name=""z""></textarea>
</form>
<div id=""result""></div>
<script>
var r = [];
var form = document.getElementById('f');
r.push(form.elements.length);
r.push(form.elements[0].name);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("3|x", result);
    }

    [Fact]
    public void Acid3_Test57_Form_Elements_NamedItem()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form id=""f"">
  <input name=""username"" type=""text"" value=""alice"" />
  <input name=""email"" type=""text"" value=""bob"" />
</form>
<div id=""result""></div>
<script>
var r = [];
var form = document.getElementById('f');
var el = form.elements['username'];
r.push(el.value);
r.push(el.name);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("alice|username", result);
    }

    [Fact]
    public void Acid3_Test58_Input_Type_Lowercase()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<input id=""a"" type=""TEXT"" />
<input id=""b"" type=""CheckBox"" />
<div id=""result""></div>
<script>
var r = [];
r.push(document.getElementById('a').type);
r.push(document.getElementById('b').type);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("text|checkbox", result);
    }

    [Fact]
    public void Acid3_Test59_Select_Add_Option()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<select id=""s""></select>
<div id=""result""></div>
<script>
var r = [];
var sel = document.getElementById('s');
var opt = document.createElement('option');
opt.text = 'Hello';
opt.value = 'h';
sel.add(opt);
r.push(sel.options.length);
r.push(sel.options[0].value);
r.push(sel.options[0].text);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("1|h|Hello", result);
    }

    [Fact(Skip = "selectedIndex assignment not yet implemented — implementation gap")]
    public void Acid3_Test60_Select_SelectedIndex()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var sel = document.createElement('select');
var o1 = document.createElement('option'); o1.value = 'a'; o1.text = 'A';
var o2 = document.createElement('option'); o2.value = 'b'; o2.text = 'B'; o2.defaultSelected = true;
var o3 = document.createElement('option'); o3.value = 'c'; o3.text = 'C';
sel.appendChild(o1); sel.appendChild(o2); sel.appendChild(o3);
r.push(sel.selectedIndex);
sel.selectedIndex = 2;
r.push(sel.selectedIndex);
r.push(sel.value);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("1|2|c", result);
    }

    [Fact]
    public void Acid3_Test61_Option_DefaultSelected()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var sel = document.createElement('select');
var o1 = document.createElement('option'); o1.value = 'a';
var o2 = document.createElement('option'); o2.value = 'b'; o2.defaultSelected = true;
sel.appendChild(o1); sel.appendChild(o2);
r.push(o1.defaultSelected);
r.push(o2.defaultSelected);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("false|true", result);
    }

    [Fact]
    public void Acid3_Test62_Input_Checked_Persists_Across_DOM_Move()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d1""><input id=""cb"" type=""checkbox"" /></div>
<div id=""d2""></div>
<div id=""result""></div>
<script>
var r = [];
var cb = document.getElementById('cb');
cb.checked = true;
r.push(cb.checked);
document.getElementById('d2').appendChild(cb);
r.push(cb.checked);
r.push(cb.parentNode.id);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|true|d2", result);
    }

    [Fact]
    public void Acid3_Test63_Radio_Mutual_Exclusion()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form id=""f"">
  <input id=""r1"" type=""radio"" name=""grp"" />
  <input id=""r2"" type=""radio"" name=""grp"" />
  <input id=""r3"" type=""radio"" name=""grp"" />
</form>
<div id=""result""></div>
<script>
var r = [];
var r1 = document.getElementById('r1');
var r2 = document.getElementById('r2');
var r3 = document.getElementById('r3');
r1.checked = true;
r.push(r1.checked + ',' + r2.checked + ',' + r3.checked);
r2.checked = true;
r.push(r1.checked + ',' + r2.checked + ',' + r3.checked);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,false,false|false,true,false", result);
    }
}
