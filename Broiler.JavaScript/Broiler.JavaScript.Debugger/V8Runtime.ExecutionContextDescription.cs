namespace Broiler.JavaScript.Debugger;


public partial class V8Runtime
{
    public class ExecutionContextDescription
    {
        public long Id { get; set; }

        public string Origin { get; set; } = "Broiler.JavaScript";

        public string Name { get; set; }

        public string UniqueId { get; set; }            
    }
}
