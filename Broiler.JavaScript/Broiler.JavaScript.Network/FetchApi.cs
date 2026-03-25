#nullable enable
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Clr;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Engine.Extensions;

namespace YantraJS.Network
{
    internal partial class FetchApi
    {

        public static async Task<JSValue> Fetch(JSContext window, HttpClient client, Arguments a)
        {
            var first = a[0] ?? throw new ArgumentNullException();
            if (!first.ConvertTo<Request>(out var request))
            {
                // build request ...
                request = new Request(a);
            }
            CancellationToken token = CancellationToken.None;
            if (request.Signal != null)
            {
                var ct = new CancellationTokenSource();
                token = ct.Token;
                request.Signal.AbortedEvent += (s, e) => {
                    ct.Cancel();
                };
            }
            var response = await client.SendAsync(request.Build(client), token);
            return new FetchResponse(request, response);
        }

    }
}
