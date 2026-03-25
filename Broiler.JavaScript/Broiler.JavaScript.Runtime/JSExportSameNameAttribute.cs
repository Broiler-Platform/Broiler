namespace Broiler.JavaScript.Runtime;

public class JSExportSameNameAttribute : JSExportAttribute
{
    public JSExportSameNameAttribute() => AsCamel = false;
}
