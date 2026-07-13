using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 fifth slice (P3.5): the HTML table DOM
/// interfaces (HTMLTableElement / HTMLTableSectionElement / HTMLTableRowElement) are now a
/// co-located binding module (<see cref="TableBinding"/>) consumed through the narrow
/// <see cref="ITableHost"/> contract. The behavior characterizations exercise the extracted
/// interface end-to-end through the bridge with no layout dependency.
/// </summary>
public sealed class TableBindingModuleTests
{
    [Fact]
    public void Table_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(TableBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.False(typeof(ITableHost).IsPublic);
    }

    [Fact]
    public void DomBridge_Consumes_Table_Through_The_Host_Contract()
    {
        Assert.True(typeof(ITableHost).IsAssignableFrom(typeof(DomBridge)));
        Assert.Contains(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(TableBinding));
    }

    [Fact]
    public void InsertRow_And_InsertCell_Build_The_Table_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<table id=""t""></table>
<script>
var t = document.getElementById('t');
var row = t.insertRow(-1);       // creates an implicit tbody + tr
var c0 = row.insertCell(-1);
c0.textContent = 'A';
var c1 = row.insertCell(-1);
c1.textContent = 'B';
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'rows=' + t.rows.length + '|cells=' + row.cells.length + '|rowIndex=' + row.rowIndex;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("rows=1|cells=2|rowIndex=0", result);
    }

    [Fact]
    public void Rows_Are_Collected_In_Section_Spec_Order_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<table id=""t"">
  <tfoot><tr id=""f""></tr></tfoot>
  <thead><tr id=""h""></tr></thead>
  <tbody><tr id=""b""></tr></tbody>
</table>
<script>
var t = document.getElementById('t');
var ids = [];
for (var i = 0; i < t.rows.length; i++) ids.push(t.rows[i].id);
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'order=' + ids.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Spec order is thead, then tbody/direct, then tfoot — regardless of source order.
        Assert.Contains("order=h,b,f", result);
    }

    [Fact]
    public void CreateTHead_And_DeleteRow_Mutate_The_Table_Through_The_Module()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<table id=""t""><tbody><tr></tr><tr></tr></tbody></table>
<script>
var t = document.getElementById('t');
var head = t.createTHead();
var again = t.createTHead();          // idempotent — returns the same <thead>
t.deleteRow(0);                        // removes the first body row
var out = document.createElement('div');
out.id = 'result';
out.textContent = 'thead=' + head.tagName.toLowerCase() + '|same=' + (head === again) + '|rows=' + t.rows.length;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("thead=thead|same=true|rows=1", result);
    }
}
