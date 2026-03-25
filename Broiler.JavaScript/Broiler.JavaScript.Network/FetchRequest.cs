#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Clr;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Extensions;
using Broiler.JavaScript.ExpressionCompiler;

namespace YantraJS.Network
{
    [JSClassGenerator]
    public partial class Request : JSObject
    {
        public Request(in Arguments a) : base(JSEngine.NewTargetPrototype)
        {
            var first = a[0] ?? throw new ArgumentNullException();
            if (first.IsString)
            {
                this.Url = first.ToString();
            } else {
                if(!first.ConvertTo<Request>(out var r))
                    throw new ArgumentException();
                this.Url = r.Url;
            }

            var options = a[1];
            JSValue v;
            v = options[Names.method];
            this.Method = v.IsNullOrUndefined ? "GET" : v.ToString();
            v = options[Names.headers];
            this.Headers = new Headers(v.IsNullOrUndefined ? null : v);
            v = options[Names.body];
            if(!v.IsNullOrUndefined)
            {
                this.Body = v;
            }
            v = options[Names.mode];
            this.Mode = v.IsNullOrUndefined ? "cors" : v.ToString();
            v = options[Names.credentials];
            this.Credentials = v.IsNullOrUndefined ? "same-origin" : v.ToString();
            v = options[Names.cache];
            this.Cache = v.IsNullOrUndefined ? null : v.ToString();
            v = options[Names.redirect];
            this.Redirect = v.IsNullOrUndefined ? "follow" : v.ToString();
            v = options[Names.referrer];
            this.Referrer = v.IsNullOrUndefined ? "about:client" : v.ToString();
            v = options[Names.referrerPolicy];
            if (!v.IsNullOrUndefined)
            {
                this.ReferrerPolicy = v.ToString();
            }
            v = options[Names.integrity];
            if (!v.IsNullOrUndefined)
                this.Integrity = v.ToString();

            v = options[Names.keepalive];
            this.KeepAlive = v.IsNullOrUndefined ? false : v.BooleanValue;
            v = options[Names.signal];
            if (!v.IsNullOrUndefined)
            {
                if(v.ConvertTo<AbortSignal>(out var s)) {
                    this.Signal = s;
                }
            }
        }

        [JSExport]
        public string Url { get; }

        [JSExport]
        public string Method { get; }

        [JSExport]
        public Headers Headers { get; }

        [JSExport]
        public JSValue? Body { get; set; }

        [JSExport]
        public string? Mode { get; set; }

        [JSExport]
        public string? Credentials { get; set; }

        [JSExport]
        public string? Cache { get; set; }

        [JSExport]
        public string? Redirect { get; set; }

        [JSExport]
        public string? Referrer { get; set; }

        [JSExport]
        public string? ReferrerPolicy { get; set; }

        [JSExport]
        public string? Integrity { get; set; }

        [JSExport]
        public bool KeepAlive { get; set; }

        [JSExport]
        public AbortSignal? Signal { get; set; }

        internal HttpRequestMessage Build(HttpClient client)
        {
            var request = new HttpRequestMessage(new HttpMethod(this.Method), this.Url);

            foreach (var header in this.Headers.GetEnumerable())
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            SetBody(request);

            return request;
        }

        private void SetBody(HttpRequestMessage request)
        {
            if (Body == null)
                return;

            if (Body.IsString)
            {
                request.Content = new StringContent(Body.ToString());
                return;
            }

            // try each type....
            if (Body.ConvertTo<KeyValueStore>(out var fd))
            {
                request.Content = new FormUrlEncodedContent(fd.GetEnumerable());
                return;
            }
        }
    }
}
