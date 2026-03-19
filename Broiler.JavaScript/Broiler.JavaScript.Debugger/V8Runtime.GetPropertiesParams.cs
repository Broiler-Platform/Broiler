namespace Broiler.JavaScript.Debugger;


public partial class V8Runtime
{
    public class GetPropertiesParams 
    {
        public string ObjectId { get; set; }

        public bool ownProperties { get; set; }
    }
}
