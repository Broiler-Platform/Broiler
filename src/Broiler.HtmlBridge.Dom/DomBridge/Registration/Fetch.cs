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
    private JSFunction RegisterFetchAndHttpApis(JSContext context, JSObject window)
    {
        static IEnumerable<(string Key, string Value)> EnumerateObjectStringEntries(JSObject obj)
        {
            foreach (var (key, value) in obj.Entries)
            {
                if (string.IsNullOrEmpty(key) || key[0] == '_' || value is JSFunction || value.IsUndefined || value.IsNull)
                    continue;

                yield return (key, value.ToString());
            }
        }
        static string? TryGetJsPropertyString(JSObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                var value = obj[(KeyString)name];
                if (value != null && !value.IsUndefined && !value.IsNull)
                    return value.ToString();
            }

            return null;
        }
        static JSObject CreateThenable(Func<JSValue> resolver)
        {
            var thenable = new JSObject();
            JSValue JsRegistrationThen077(in Arguments a)
            {
                if (a.Length > 0 && a[0] is JSFunction cb)
                    cb.InvokeFunction(new Arguments(cb, resolver()));
                return thenable;
            }
            thenable.FastAddValue((KeyString)"then", new JSFunction(JsRegistrationThen077, "then", 1), JSPropertyAttributes.EnumerableConfigurableValue);

            return thenable;
        }
        static JSObject CreateHeadersObject(JSValue? initValue = null)
        {
            var headersObject = new JSObject();
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var originalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void SyncHeader(string name)
            {
                if (!values.TryGetValue(name, out var currentValue))
                    currentValue = string.Empty;

                var originalName = originalNames.TryGetValue(name, out var storedName) ? storedName : name;
                headersObject[(KeyString)originalName] = new JSString(currentValue);
                headersObject[(KeyString)name.ToLowerInvariant()] = new JSString(currentValue);
            }

            void SetHeader(string name, string value)
            {
                values[name] = value;
                originalNames[name] = name;
                SyncHeader(name);
            }

            void AppendHeader(string name, string value)
            {
                if (values.TryGetValue(name, out var existing) && !string.IsNullOrEmpty(existing))
                    values[name] = $"{existing}, {value}";
                else
                    values[name] = value;

                originalNames[name] = name;
                SyncHeader(name);
            }

            if (initValue is JSObject initObject)
            {
                foreach (var (key, value) in EnumerateObjectStringEntries(initObject))
                    AppendHeader(key, value);
            }
            JSValue JsRegistrationGet078(in Arguments a)
            {
                if (a.Length == 0)
                    return JSNull.Value;
                var name = a[0].ToString();
                return values.TryGetValue(name, out var currentValue) ? new JSString(currentValue) : JSNull.Value;
            }

            headersObject.FastAddValue((KeyString)"get", new JSFunction(JsRegistrationGet078, "get", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationHas079(in Arguments a)
            {
                if (a.Length == 0)
                    return JSBoolean.False;
                return values.ContainsKey(a[0].ToString()) ? JSBoolean.True : JSBoolean.False;
            }
            headersObject.FastAddValue((KeyString)"has", new JSFunction(JsRegistrationHas079, "has", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationSet080(in Arguments a)
            {
                if (a.Length >= 2)
                    SetHeader(a[0].ToString(), a[1].ToString());
                return JSUndefined.Value;
            }
            headersObject.FastAddValue((KeyString)"set", new JSFunction(JsRegistrationSet080, "set", 2), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationAppend081(in Arguments a)
            {
                if (a.Length >= 2)
                    AppendHeader(a[0].ToString(), a[1].ToString());
                return JSUndefined.Value;
            }
            headersObject.FastAddValue((KeyString)"append", new JSFunction(JsRegistrationAppend081, "append", 2), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationDelete082(in Arguments a)
            {
                if (a.Length > 0)
                {
                    var name = a[0].ToString();
                    values.Remove(name);
                    originalNames.Remove(name);
                    headersObject[(KeyString)name] = JSUndefined.Value;
                    headersObject[(KeyString)name.ToLowerInvariant()] = JSUndefined.Value;
                }

                return JSUndefined.Value;
            }
            headersObject.FastAddValue((KeyString)"delete", new JSFunction(JsRegistrationDelete082, "delete", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationForEach083(in Arguments a)
            {
                if (a.Length > 0 && a[0] is JSFunction cb)
                {
                    foreach (var header in values)
                    {
                        var name = originalNames.TryGetValue(header.Key, out var originalName) ? originalName : header.Key;
                        cb.InvokeFunction(new Arguments(cb, new JSString(header.Value), new JSString(name), headersObject));
                    }
                }

                return JSUndefined.Value;
            }
            headersObject.FastAddValue((KeyString)"forEach", new JSFunction(JsRegistrationForEach083, "forEach", 1), JSPropertyAttributes.EnumerableConfigurableValue);

            return headersObject;
        }
        static JSValue ParseJsonText(string jsonText)
            => JSJSON.Parse(new Arguments(JSUndefined.Value, new JSString(jsonText)));
        static JSValue ParseResponseJsonText(string jsonText)
        {
            try
            {
                return ParseJsonText(jsonText);
            }
            catch (Exception ex)
            {
                throw new JSException($"Failed to parse response body as JSON: {ex.Message}");
            }
        }
        static string DecodeFormComponent(string value)
            => Uri.UnescapeDataString(value.Replace("+", " "));
        static bool IsFormComponentUnescapedByte(byte value)
            => (value >= (byte)'a' && value <= (byte)'z')
               || (value >= (byte)'A' && value <= (byte)'Z')
               || (value >= (byte)'0' && value <= (byte)'9')
               || value is (byte)'*' or (byte)'-' or (byte)'.' or (byte)'_';
        static string EncodeFormComponent(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var builder = new StringBuilder(bytes.Length);
            foreach (var current in bytes)
            {
                if (current == (byte)' ')
                {
                    builder.Append('+');
                }
                else if (IsFormComponentUnescapedByte(current))
                {
                    builder.Append((char)current);
                }
                else
                {
                    builder.Append('%');
                    builder.Append(current.ToString("X2"));
                }
            }

            return builder.ToString();
        }
        static JSObject CreateFormDataObject(JSValue? initValue = null)
        {
            var formDataObject = new JSObject();
            var entries = new List<KeyValuePair<string, string>>();

            void AppendEntry(string name, string value)
                => entries.Add(new KeyValuePair<string, string>(name, value));

            void SetEntry(string name, string value)
            {
                var firstIndex = -1;
                for (var i = 0; i < entries.Count; i++)
                {
                    if (!string.Equals(entries[i].Key, name, StringComparison.Ordinal))
                        continue;

                    if (firstIndex < 0)
                    {
                        firstIndex = i;
                        entries[i] = new KeyValuePair<string, string>(name, value);
                    }
                    else
                    {
                        entries.RemoveAt(i);
                        i--;
                    }
                }

                if (firstIndex < 0)
                    entries.Add(new KeyValuePair<string, string>(name, value));
            }

            if (initValue != null && !initValue.IsUndefined && !initValue.IsNull)
            {
                if (initValue is JSObject initObject)
                {
                    foreach (var (key, value) in EnumerateObjectStringEntries(initObject))
                        AppendEntry(key, value);
                }
                else
                {
                    var initText = initValue.ToString();
                    if (!string.IsNullOrEmpty(initText))
                    {
                        foreach (var segment in initText.Split('&', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var separatorIndex = segment.IndexOf('=');
                            var rawName = separatorIndex >= 0 ? segment[..separatorIndex] : segment;
                            var rawValue = separatorIndex >= 0 ? segment[(separatorIndex + 1)..] : string.Empty;
                            AppendEntry(DecodeFormComponent(rawName), DecodeFormComponent(rawValue));
                        }
                    }
                }
            }
            JSValue JsRegistrationAppend084(in Arguments a)
            {
                if (a.Length >= 2)
                    AppendEntry(a[0].ToString(), a[1].ToString());
                return JSUndefined.Value;
            }

            formDataObject.FastAddValue((KeyString)"append", new JSFunction(JsRegistrationAppend084, "append", 2), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationDelete085(in Arguments a)
            {
                if (a.Length > 0)
                {
                    var name = a[0].ToString();
                    entries.RemoveAll(entry => string.Equals(entry.Key, name, StringComparison.Ordinal));
                }

                return JSUndefined.Value;
            }
            formDataObject.FastAddValue((KeyString)"delete", new JSFunction(JsRegistrationDelete085, "delete", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationForEach086(in Arguments a)
            {
                if (a.Length > 0 && a[0] is JSFunction cb)
                {
                    foreach (var entry in entries)
                        cb.InvokeFunction(new Arguments(cb, new JSString(entry.Value), new JSString(entry.Key), formDataObject));
                }

                return JSUndefined.Value;
            }
            formDataObject.FastAddValue((KeyString)"forEach", new JSFunction(JsRegistrationForEach086, "forEach", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationGet087(in Arguments a)
            {
                if (a.Length == 0)
                    return JSNull.Value;
                var name = a[0].ToString();
                foreach (var entry in entries)
                {
                    if (string.Equals(entry.Key, name, StringComparison.Ordinal))
                        return new JSString(entry.Value);
                }

                return JSNull.Value;
            }
            formDataObject.FastAddValue((KeyString)"get", new JSFunction(JsRegistrationGet087, "get", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationGetAll088(in Arguments a)
            {
                var result = new JSArray();
                if (a.Length == 0)
                    return result;
                var name = a[0].ToString();
                foreach (var entry in entries)
                {
                    if (string.Equals(entry.Key, name, StringComparison.Ordinal))
                        result.Add(new JSString(entry.Value));
                }

                return result;
            }
            formDataObject.FastAddValue((KeyString)"getAll", new JSFunction(JsRegistrationGetAll088, "getAll", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationHas089(in Arguments a)
            {
                if (a.Length == 0)
                    return JSBoolean.False;
                var name = a[0].ToString();
                return entries.Any(entry => string.Equals(entry.Key, name, StringComparison.Ordinal)) ? JSBoolean.True : JSBoolean.False;
            }
            formDataObject.FastAddValue((KeyString)"has", new JSFunction(JsRegistrationHas089, "has", 1), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationSet090(in Arguments a)
            {
                if (a.Length >= 2)
                    SetEntry(a[0].ToString(), a[1].ToString());
                return JSUndefined.Value;
            }
            formDataObject.FastAddValue((KeyString)"set", new JSFunction(JsRegistrationSet090, "set", 2), JSPropertyAttributes.EnumerableConfigurableValue);
            formDataObject.FastAddValue((KeyString)"toString", new JSFunction((in Arguments _) => new JSString(string.Join("&", entries.Select(static entry => $"{EncodeFormComponent(entry.Key)}={EncodeFormComponent(entry.Value)}"))),
                "toString", 0), JSPropertyAttributes.EnumerableConfigurableValue);

            return formDataObject;
        }
        static JSValue CreateBlobBody(string bodyText, JSObject headersObject)
        {
            var contentType = TryGetJsPropertyString(headersObject, "content-type", "Content-Type") ?? string.Empty;
            var blobObject = new JSObject();
            blobObject[(KeyString)"size"] = new JSNumber(Encoding.UTF8.GetByteCount(bodyText));
            blobObject[(KeyString)"type"] = new JSString(contentType);
            blobObject.FastAddValue((KeyString)"text", new JSFunction((in Arguments _) => CreateThenable(() => new JSString(bodyText)), "text", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            blobObject.FastAddValue((KeyString)"arrayBuffer", new JSFunction((in Arguments _) => CreateThenable(() => new JSArrayBuffer(Encoding.UTF8.GetBytes(bodyText))), "arrayBuffer", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            return blobObject;
        }
        static bool IsBodyUnavailable(JSObject owner)
            => (owner[(KeyString)"bodyUsed"]?.BooleanValue ?? false) || (owner[(KeyString)"_bodyStreamLocked"]?.BooleanValue ?? false);
        static JSObject CreateReadableStreamReadResult(JSValue value, bool done)
        {
            var result = new JSObject();
            result[(KeyString)"value"] = value;
            result[(KeyString)"done"] = done ? JSBoolean.True : JSBoolean.False;
            return result;
        }
        JSValue CreateUint8Array(byte[] bytes)
        {
            if (context[(KeyString)"Uint8Array"] is JSFunction uint8ArrayCtor)
                return uint8ArrayCtor.CreateInstance(new Arguments(JSUndefined.Value, new JSArrayBuffer(bytes)));

            return new JSArrayBuffer(bytes);
        }
        JSObject CreateReadableStreamBody(JSObject owner, string bodyText)
        {
            var bytes = Encoding.UTF8.GetBytes(bodyText);
            var streamObject = new JSObject();
            var readerLocked = false;
            var chunkDelivered = false;

            void SetLocked(bool locked)
            {
                readerLocked = locked;
                var lockedValue = locked ? JSBoolean.True : JSBoolean.False;
                owner[(KeyString)"_bodyStreamLocked"] = lockedValue;
                streamObject[(KeyString)"locked"] = lockedValue;
            }

            streamObject[(KeyString)"locked"] = JSBoolean.False;
            JSValue JsRegistrationGetReader097(in Arguments _)
            {
                if (IsBodyUnavailable(owner) || readerLocked)
                    throw new JSException("Failed to execute 'getReader' on 'ReadableStream': stream is already locked or disturbed.");
                SetLocked(true);
                var readerObject = new JSObject();
                JSValue JsRegistrationRead094(in Arguments _)
                {
                    if (!readerLocked)
                        throw new JSException("Failed to execute 'read' on 'ReadableStreamDefaultReader': reader is released.");
                    if (chunkDelivered)
                        return CreateThenable(() => CreateReadableStreamReadResult(JSUndefined.Value, true));
                    chunkDelivered = true;
                    owner[(KeyString)"bodyUsed"] = JSBoolean.True;
                    return CreateThenable(() => CreateReadableStreamReadResult(CreateUint8Array(bytes), false));
                }

                readerObject.FastAddValue((KeyString)"read", new JSFunction(JsRegistrationRead094, "read", 0), JSPropertyAttributes.EnumerableConfigurableValue);
                JSValue JsRegistrationCancel095(in Arguments _)
                {
                    if (readerLocked && !chunkDelivered)
                        owner[(KeyString)"bodyUsed"] = JSBoolean.True;
                    chunkDelivered = true;
                    return CreateThenable(() => JSUndefined.Value);
                }

                readerObject.FastAddValue((KeyString)"cancel", new JSFunction(JsRegistrationCancel095, "cancel", 1), JSPropertyAttributes.EnumerableConfigurableValue);
                JSValue JsRegistrationReleaseLock096(in Arguments _)
                {
                    SetLocked(false);
                    return JSUndefined.Value;
                }

                readerObject.FastAddValue((KeyString)"releaseLock", new JSFunction(JsRegistrationReleaseLock096, "releaseLock", 0), JSPropertyAttributes.EnumerableConfigurableValue);
                return readerObject;
            }
            streamObject.FastAddValue((KeyString)"getReader", new JSFunction(JsRegistrationGetReader097, "getReader", 0), JSPropertyAttributes.EnumerableConfigurableValue);

            return streamObject;
        }
        JSObject CreateRequestObject(JSValue inputValue, JSValue? initValue = null)
        {
            string url;
            string method;
            string? body;
            JSObject headersObject;
            JSValue signalValue = JSUndefined.Value;
            string mode = "cors";
            string credentials = "same-origin";
            string cache = "default";
            string redirect = "follow";
            string referrer = "about:client";
            string integrity = string.Empty;

            if (inputValue is JSObject inputObject && !string.IsNullOrEmpty(TryGetJsPropertyString(inputObject, "url", "href")))
            {
                url = TryGetJsPropertyString(inputObject, "url", "href") ?? string.Empty;
                method = (TryGetJsPropertyString(inputObject, "method") ?? "GET").ToUpperInvariant();
                body = TryGetJsPropertyString(inputObject, "_bodyInit", "body");
                headersObject = inputObject[(KeyString)"headers"] is JSObject inputHeaders
                    ? CreateHeadersObject(inputHeaders)
                    : CreateHeadersObject();
                signalValue = inputObject[(KeyString)"signal"] ?? JSUndefined.Value;
                mode = TryGetJsPropertyString(inputObject, "mode") ?? mode;
                credentials = TryGetJsPropertyString(inputObject, "credentials") ?? credentials;
                cache = TryGetJsPropertyString(inputObject, "cache") ?? cache;
                redirect = TryGetJsPropertyString(inputObject, "redirect") ?? redirect;
                referrer = TryGetJsPropertyString(inputObject, "referrer") ?? referrer;
                integrity = TryGetJsPropertyString(inputObject, "integrity") ?? integrity;
            }
            else
            {
                url = inputValue.ToString();
                method = "GET";
                body = null;
                headersObject = CreateHeadersObject();
            }

            if (initValue is JSObject initObject)
            {
                method = (TryGetJsPropertyString(initObject, "method") ?? method).ToUpperInvariant();
                if (TryGetJsPropertyString(initObject, "body") is string initBody)
                    body = initBody;
                if (initObject[(KeyString)"headers"] is JSObject initHeaders)
                    headersObject = CreateHeadersObject(initHeaders);
                if (initObject[(KeyString)"signal"] is { } initSignal && !initSignal.IsUndefined && !initSignal.IsNull)
                    signalValue = initSignal;
                mode = TryGetJsPropertyString(initObject, "mode") ?? mode;
                credentials = TryGetJsPropertyString(initObject, "credentials") ?? credentials;
                cache = TryGetJsPropertyString(initObject, "cache") ?? cache;
                redirect = TryGetJsPropertyString(initObject, "redirect") ?? redirect;
                referrer = TryGetJsPropertyString(initObject, "referrer") ?? referrer;
                integrity = TryGetJsPropertyString(initObject, "integrity") ?? integrity;
            }

            var requestObject = new JSObject();
            requestObject[(KeyString)"url"] = new JSString(url);
            requestObject[(KeyString)"method"] = new JSString(method);
            requestObject[(KeyString)"headers"] = headersObject;
            requestObject[(KeyString)"bodyUsed"] = JSBoolean.False;
            requestObject[(KeyString)"_bodyStreamLocked"] = JSBoolean.False;
            requestObject[(KeyString)"_bodyInit"] = body == null ? JSNull.Value : new JSString(body);
            requestObject[(KeyString)"body"] = body == null ? JSNull.Value : CreateReadableStreamBody(requestObject, body);
            requestObject[(KeyString)"signal"] = signalValue;
            requestObject[(KeyString)"mode"] = new JSString(mode);
            requestObject[(KeyString)"credentials"] = new JSString(credentials);
            requestObject[(KeyString)"cache"] = new JSString(cache);
            requestObject[(KeyString)"redirect"] = new JSString(redirect);
            requestObject[(KeyString)"referrer"] = new JSString(referrer);
            requestObject[(KeyString)"integrity"] = new JSString(integrity);
            JSValue JsRegistrationClone098(in Arguments _)
            {
                if (IsBodyUnavailable(requestObject))
                    throw new JSException("Failed to execute 'clone' on 'Request': body is already used.");
                return CreateRequestObject(requestObject);
            }
            requestObject.FastAddValue((KeyString)"clone", new JSFunction(JsRegistrationClone098, "clone", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationText099(in Arguments _)
            {
                if (IsBodyUnavailable(requestObject))
                    throw new JSException("Failed to execute body reader on 'Request': body is already used.");
                requestObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                return CreateThenable(() => body == null ? new JSString(string.Empty) : new JSString(body));
            }
            requestObject.FastAddValue((KeyString)"text", new JSFunction(JsRegistrationText099, "text", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationJson100(in Arguments _)
            {
                if (IsBodyUnavailable(requestObject))
                    throw new JSException("Failed to execute body reader on 'Request': body is already used.");
                requestObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                return CreateThenable(() => ParseJsonText(body ?? string.Empty));
            }
            requestObject.FastAddValue((KeyString)"json", new JSFunction(JsRegistrationJson100, "json", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationArrayBuffer101(in Arguments _)
            {
                if (IsBodyUnavailable(requestObject))
                    throw new JSException("Failed to execute body reader on 'Request': body is already used.");
                requestObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                return CreateThenable(() => new JSArrayBuffer(Encoding.UTF8.GetBytes(body ?? string.Empty)));
            }
            requestObject.FastAddValue((KeyString)"arrayBuffer", new JSFunction(JsRegistrationArrayBuffer101, "arrayBuffer", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationBlob102(in Arguments _)
            {
                if (IsBodyUnavailable(requestObject))
                    throw new JSException("Failed to execute body reader on 'Request': body is already used.");
                requestObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                return CreateThenable(() => CreateBlobBody(body ?? string.Empty, headersObject));
            }
            requestObject.FastAddValue((KeyString)"blob", new JSFunction(JsRegistrationBlob102, "blob", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationFormData103(in Arguments _)
            {
                if (IsBodyUnavailable(requestObject))
                    throw new JSException("Failed to execute body reader on 'Request': body is already used.");
                requestObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                return CreateThenable(() => CreateFormDataObject(new JSString(body ?? string.Empty)));
            }
            requestObject.FastAddValue((KeyString)"formData", new JSFunction(JsRegistrationFormData103, "formData", 0), JSPropertyAttributes.EnumerableConfigurableValue);

            return requestObject;
        }
        JSValue CreateResponse(string body, int statusCode, string statusText, string responseUrl, string type, bool redirected, Dictionary<string, string> headers)
        {
            var responseHeaders = new JSObject();
            foreach (var header in headers)
                responseHeaders[(KeyString)header.Key] = new JSString(header.Value);

            var headersObject = CreateHeadersObject(responseHeaders);
            var responseObject = new JSObject();
            responseObject[(KeyString)"ok"] = statusCode >= 200 && statusCode < 300 ? JSBoolean.True : JSBoolean.False;
            responseObject[(KeyString)"status"] = new JSNumber(statusCode);
            responseObject[(KeyString)"statusText"] = new JSString(statusText);
            responseObject[(KeyString)"url"] = new JSString(responseUrl);
            responseObject[(KeyString)"redirected"] = redirected ? JSBoolean.True : JSBoolean.False;
            responseObject[(KeyString)"type"] = new JSString(type);
            responseObject[(KeyString)"bodyUsed"] = JSBoolean.False;
            responseObject[(KeyString)"_bodyStreamLocked"] = JSBoolean.False;
            responseObject[(KeyString)"headers"] = headersObject;
            responseObject[(KeyString)"_bodyText"] = new JSString(body);
            responseObject[(KeyString)"body"] = CreateReadableStreamBody(responseObject, body);
            JSValue JsRegistrationText104(in Arguments _)
            {
                if (IsBodyUnavailable(responseObject))
                    throw new JSException("Failed to execute body reader on 'Response': body is already used.");
                responseObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                return CreateThenable(() => new JSString(body));
            }
            responseObject.FastAddValue((KeyString)"text", new JSFunction(JsRegistrationText104, "text", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationJson105(in Arguments _)
            {
                if (IsBodyUnavailable(responseObject))
                    throw new JSException("Failed to execute body reader on 'Response': body is already used.");
                responseObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                return CreateThenable(() => ParseResponseJsonText(body));
            }
            responseObject.FastAddValue((KeyString)"json", new JSFunction(JsRegistrationJson105, "json", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationArrayBuffer106(in Arguments _)
            {
                if (IsBodyUnavailable(responseObject))
                    throw new JSException("Failed to execute body reader on 'Response': body is already used.");
                responseObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                return CreateThenable(() => new JSArrayBuffer(Encoding.UTF8.GetBytes(body)));
            }
            responseObject.FastAddValue((KeyString)"arrayBuffer", new JSFunction(JsRegistrationArrayBuffer106, "arrayBuffer", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationBlob107(in Arguments _)
            {
                if (IsBodyUnavailable(responseObject))
                    throw new JSException("Failed to execute body reader on 'Response': body is already used.");
                responseObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                return CreateThenable(() => CreateBlobBody(body, headersObject));
            }
            responseObject.FastAddValue((KeyString)"blob", new JSFunction(JsRegistrationBlob107, "blob", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationFormData108(in Arguments _)
            {
                if (IsBodyUnavailable(responseObject))
                    throw new JSException("Failed to execute body reader on 'Response': body is already used.");
                responseObject[(KeyString)"bodyUsed"] = JSBoolean.True;
                return CreateThenable(() => CreateFormDataObject(new JSString(body)));
            }
            responseObject.FastAddValue((KeyString)"formData", new JSFunction(JsRegistrationFormData108, "formData", 0), JSPropertyAttributes.EnumerableConfigurableValue);
            JSValue JsRegistrationClone109(in Arguments _)
            {
                if (IsBodyUnavailable(responseObject))
                    throw new JSException("Failed to execute 'clone' on 'Response': body is already used.");
                return CreateResponse(body, statusCode, statusText, responseUrl, type, redirected, new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase));
            }
            responseObject.FastAddValue((KeyString)"clone", new JSFunction(JsRegistrationClone109, "clone", 0), JSPropertyAttributes.EnumerableConfigurableValue);

            return responseObject;
        }
        static JSValue CreateAbortErrorValue(JSValue signalValue)
        {
            if (signalValue is JSObject signalObject)
            {
                var reason = signalObject[(KeyString)"reason"];
                if (reason != null && !reason.IsUndefined && !reason.IsNull)
                    return reason;
            }

            var error = new JSObject();
            error[(KeyString)"name"] = new JSString("AbortError");
            error[(KeyString)"message"] = new JSString("The operation was aborted.");
            return error;
        }
        (int status, string statusText, string url, string type, bool redirected, Dictionary<string, string> headers) ParseResponseInit(JSValue? initValue)
        {
            var status = 200;
            var statusText = string.Empty;
            var url = string.Empty;
            var type = "basic";
            var redirected = false;
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (initValue is JSObject initObject)
            {
                if (TryGetJsPropertyString(initObject, "status") is string statusValue && int.TryParse(statusValue, out var parsedStatus))
                    status = parsedStatus;
                statusText = TryGetJsPropertyString(initObject, "statusText") ?? string.Empty;
                url = TryGetJsPropertyString(initObject, "url") ?? string.Empty;
                type = TryGetJsPropertyString(initObject, "type") ?? "basic";
                redirected = string.Equals(TryGetJsPropertyString(initObject, "redirected"), "true", StringComparison.OrdinalIgnoreCase);

                if (initObject[(KeyString)"headers"] is JSObject initHeaders)
                {
                    foreach (var (key, value) in EnumerateObjectStringEntries(initHeaders))
                        headers[key] = value;
                }
            }

            return (status, statusText, url, type, redirected, headers);
        }
        string ResolveResponseRedirectUrl(string redirectUrl)
        {
            if (string.IsNullOrWhiteSpace(redirectUrl))
                throw new JSException("Failed to execute 'redirect' on 'Response': Invalid URL");

            if (Uri.TryCreate(redirectUrl, UriKind.Absolute, out var absoluteUri))
                return absoluteUri.AbsoluteUri;

            if (Uri.TryCreate(_pageUrl, UriKind.Absolute, out var baseUri) &&
                Uri.TryCreate(baseUri, redirectUrl, out var resolvedUri))
            {
                return resolvedUri.AbsoluteUri;
            }

            throw new JSException("Failed to execute 'redirect' on 'Response': Invalid URL");
        }
        var formDataCtor = new JSFunction((in Arguments a) => CreateFormDataObject(a.Length > 0 ? a[0] : null), "FormData", 1);
        var headersCtor = new JSFunction((in Arguments a) => CreateHeadersObject(a.Length > 0 ? a[0] : null), "Headers", 1);
        var requestCtor = new JSFunction((in Arguments a) => CreateRequestObject(a.Length > 0 ? a[0] : JSUndefined.Value, a.Length > 1 ? a[1] : null), "Request", 2);
        var responseCtor = new JSFunction((in Arguments a) => JsRegistrationResponse113Core(ParseResponseInit, CreateResponse, in a), "Response", 2);
        responseCtor.FastAddValue((KeyString)"json", new JSFunction((in Arguments a) => JsRegistrationJson114Core(ParseResponseInit, CreateResponse, in a), "json", 2), JSPropertyAttributes.EnumerableConfigurableValue);
        responseCtor.FastAddValue((KeyString)"error", new JSFunction((in Arguments _) => CreateResponse(string.Empty, 0, string.Empty, string.Empty, "error", false, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            "error", 0), JSPropertyAttributes.EnumerableConfigurableValue);
        responseCtor.FastAddValue((KeyString)"redirect", new JSFunction((in Arguments a) => JsRegistrationRedirect116Core(ResolveResponseRedirectUrl, CreateResponse, in a), "redirect", 2), JSPropertyAttributes.EnumerableConfigurableValue);
        var messageChannelCtor = new JSFunction((in Arguments _) => CreateMessageChannel(), "MessageChannel", 0);
        window.FastAddValue((KeyString)"FormData", formDataCtor, JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"Headers", headersCtor, JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"Request", requestCtor, JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"Response", responseCtor, JSPropertyAttributes.EnumerableConfigurableValue);
        window.FastAddValue((KeyString)"MessageChannel", messageChannelCtor, JSPropertyAttributes.EnumerableConfigurableValue);
        context["FormData"] = formDataCtor;
        context["Headers"] = headersCtor;
        context["Request"] = requestCtor;
        context["Response"] = responseCtor;
        context["MessageChannel"] = messageChannelCtor;
        // fetch(url, options) — polyfill backed by HttpClient with headers, method support
        var fetchFn = new JSFunction((in Arguments a) => JsRegistrationFetch120Core(TryGetJsPropertyString, EnumerateObjectStringEntries, CreateAbortErrorValue, CreateResponse, in a), "fetch", 1);
        window.FastAddValue((KeyString)"fetch", fetchFn, JSPropertyAttributes.EnumerableConfigurableValue);
        // window.getComputedStyle(element, pseudoElement)
        var bridgeForStyle = this;
        window.FastAddValue(
            (KeyString)"getComputedStyle",
            new JSFunction((in Arguments a) => JsRegistrationGetComputedStyle121Core(bridgeForStyle, in a), "getComputedStyle", 2),
            JSPropertyAttributes.EnumerableConfigurableValue);
        // XMLHttpRequest — basic polyfill backed by HttpClient
        RegisterXMLHttpRequest(context);
        return fetchFn;
    }

}
