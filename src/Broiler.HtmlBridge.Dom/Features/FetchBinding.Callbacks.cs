using System.Net.Http;
using System.Text;
using Broiler.HtmlBridge.Logging;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.Json;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The <c>Response</c> static factories (<c>new Response</c> / <c>Response.json</c> /
/// <c>Response.redirect</c>) and the <c>fetch</c> network call, moved out of the bridge's shared
/// registration callbacks into the co-located <see cref="FetchBinding"/> module (P3.11). The fetch
/// implementation performs its host I/O through the injected <see cref="Broiler.HtmlBridge.Dom.Runtime.ResourceLoader"/>.
/// </summary>
internal sealed partial class FetchBinding
{
    private JSValue JsRegistrationResponse113Core(ResponseInitParser parseResponseInit, ResponseFactory createResponse, in Arguments a)
    {
        var body = a.Length > 0 && !a[0].IsUndefined && !a[0].IsNull ? a[0].ToString() : string.Empty;
        var (status, statusText, url, type, redirected, headers) = parseResponseInit(a.Length > 1 ? a[1] : null);
        return createResponse(body, status, statusText, url, type, redirected, headers);
    }


    private JSValue JsRegistrationJson114Core(ResponseInitParser parseResponseInit, ResponseFactory createResponse, in Arguments a)
    {
        var jsonBody = JSJSON.Stringify(a.Length > 0 ? a[0] : JSNull.Value);
        var (status, statusText, url, type, redirected, headers) = parseResponseInit(a.Length > 1 ? a[1] : null);
        if (!headers.ContainsKey("Content-Type"))
            headers["Content-Type"] = "application/json";
        return createResponse(jsonBody, status, statusText, url, type, redirected, headers);
    }


    private JSValue JsRegistrationRedirect116Core(Func<string, string> resolveResponseRedirectUrl, ResponseFactory createResponse, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'redirect' on 'Response': 1 argument required.");
        var status = 302;
        if (a.Length > 1 && int.TryParse(a[1].ToString(), out var parsedStatus))
            status = parsedStatus;
        if (status is not (301 or 302 or 303 or 307 or 308))
            throw new JSException("Failed to execute 'redirect' on 'Response': Invalid status code");
        var resolvedUrl = resolveResponseRedirectUrl(a[0].ToString());
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Location"] = resolvedUrl
        };
        return createResponse(string.Empty, status, string.Empty, string.Empty, "basic", false, headers);
    }


    private JSValue JsRegistrationFetch120Core(JsPropertyStringGetter tryGetJsPropertyString, ObjectStringEntriesEnumerator enumerateObjectStringEntries, Func<JSValue, JSValue> createAbortErrorValue, ResponseFactory createResponse, in Arguments a)
    {
        if (a.Length == 0)
            throw new JSException("Failed to execute 'fetch': 1 argument required.");
        var fetchUrl = a[0].ToString();
        if (a[0] is JSObject requestInput)
        {
            fetchUrl = tryGetJsPropertyString(requestInput, "url", "href") ?? fetchUrl;
        }

        JSValue responseObj = new JSObject();
        // Parse options (method, headers, body)
        var method = "GET";
        string? requestBody = null;
        var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        JSValue signalValue = JSUndefined.Value;
        if (a[0] is JSObject requestObject)
        {
            method = (tryGetJsPropertyString(requestObject, "method") ?? method).ToUpperInvariant();
            requestBody = tryGetJsPropertyString(requestObject, "_bodyInit", "body");
            if (requestObject[(KeyString)"signal"] is { } requestSignal && !requestSignal.IsUndefined && !requestSignal.IsNull)
                signalValue = requestSignal;
            if (requestObject[(KeyString)"headers"] is JSObject requestHeadersObject)
            {
                foreach (var (key, value) in enumerateObjectStringEntries(requestHeadersObject))
                    requestHeaders[key] = value;
            }
        }

        if (a.Length > 1 && a[1] is JSObject opts)
        {
            method = (tryGetJsPropertyString(opts, "method") ?? method).ToUpperInvariant();
            requestBody = tryGetJsPropertyString(opts, "body") ?? requestBody;
            if (opts[(KeyString)"signal"] is { } optionsSignal && !optionsSignal.IsUndefined && !optionsSignal.IsNull)
                signalValue = optionsSignal;
            if (opts[(KeyString)"headers"] is JSObject optionsHeadersObject)
            {
                foreach (var (key, value) in enumerateObjectStringEntries(optionsHeadersObject))
                    requestHeaders[key] = value;
            }
        }

        var rejected = false;
        var rejectedValue = JSUndefined.Value;
        if (signalValue is JSObject signalObject && signalObject[(KeyString)"aborted"].BooleanValue)
        {
            rejected = true;
            rejectedValue = createAbortErrorValue(signalValue);
        }

        try
        {
            if (!rejected)
            {
                var request = new HttpRequestMessage(new HttpMethod(method), fetchUrl);
                if (requestBody != null)
                    request.Content = new StringContent(requestBody, Encoding.UTF8, requestHeaders.TryGetValue("Content-Type", out var ct) ? ct : "text/plain");
                foreach (var kv in requestHeaders)
                {
                    if (!string.Equals(kv.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                        request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }

                var response = _resources.SendAsync(request).GetAwaiter().GetResult();
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var statusCode = (int)response.StatusCode;
                var allHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in response.Headers)
                    allHeaders[h.Key] = string.Join(", ", h.Value);
                if (response.Content.Headers != null)
                {
                    foreach (var h in response.Content.Headers)
                        allHeaders[h.Key] = string.Join(", ", h.Value);
                }

                responseObj = createResponse(body, statusCode, response.ReasonPhrase ?? string.Empty, fetchUrl, "basic", false, allHeaders);
            }
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.JavaScript, "DomBridge.fetch", $"Fetch error: {ex.Message}", ex);
            responseObj = createResponse(string.Empty, 0, ex.Message, fetchUrl, "error", false, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        // Return a thenable (Promise-like) that resolves immediately
        var promise = new JSObject();
        JSValue JsRegistrationThen118(in Arguments thenArgs)
        {
            if (!rejected && thenArgs.Length > 0 && thenArgs[0] is JSFunction cb)
            {
                try
                {
                    cb.InvokeFunction(new Arguments(cb, responseObj));
                }
                catch (Exception ex)
                {
                    RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.fetch.then", $"Callback error: {ex.Message}", ex);
                }
            }

            return promise;
        }

        promise.FastAddValue((KeyString)"then", new JSFunction(JsRegistrationThen118, "then", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        JSValue JsRegistrationCatch119(in Arguments catchArgs)
        {
            if (rejected && catchArgs.Length > 0 && catchArgs[0] is JSFunction cb)
            {
                try
                {
                    cb.InvokeFunction(new Arguments(cb, rejectedValue));
                }
                catch (Exception ex)
                {
                    RenderLogger.LogWarning(LogCategory.JavaScript, "DomBridge.fetch.catch", $"Callback error: {ex.Message}", ex);
                }
            }

            return promise;
        }

        promise.FastAddValue((KeyString)"catch", new JSFunction(JsRegistrationCatch119, "catch", 1), JSPropertyAttributes.EnumerableConfigurableValue);
        return promise;
    }
}
