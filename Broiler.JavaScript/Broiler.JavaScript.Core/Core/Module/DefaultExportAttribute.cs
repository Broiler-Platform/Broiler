using System;

namespace Broiler.JavaScript.Core.Core.Module;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class DefaultExportAttribute: ExportAttribute
{
    public DefaultExportAttribute(): base("default")
    {

    }
}
