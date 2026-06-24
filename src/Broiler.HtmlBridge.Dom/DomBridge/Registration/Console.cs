using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Json;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    /// <summary>
    /// Builds a <c>console</c> object exposing <c>log</c>, <c>warn</c>,
    /// <c>error</c>, and <c>info</c>.
    /// </summary>
    private static JSObject BuildConsoleObject()
    {
        var console = new JSObject();

        console.FastAddValue(
            (KeyString)"log",
            new JSFunction(JsRegistrationLog156Core, "log"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        console.FastAddValue(
            (KeyString)"warn",
            new JSFunction(JsRegistrationWarn157Core, "warn"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        console.FastAddValue(
            (KeyString)"error",
            new JSFunction(JsRegistrationError158Core, "error"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        console.FastAddValue(
            (KeyString)"info",
            new JSFunction(JsRegistrationInfo159Core, "info"),
            JSPropertyAttributes.EnumerableConfigurableValue);

        return console;
    }

}
