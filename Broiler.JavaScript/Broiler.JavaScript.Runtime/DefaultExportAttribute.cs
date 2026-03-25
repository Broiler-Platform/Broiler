using System;

namespace Broiler.JavaScript.Runtime;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class DefaultExportAttribute : ExportAttribute
{
    public DefaultExportAttribute() : base("default")
    {
    }
}
