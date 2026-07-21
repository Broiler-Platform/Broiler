// Broiler HtmlBridge — content-rendering polyfills
// version: 1  (Google Search Compliance content-rendering / fidelity stubs)
//
// Pure-JavaScript polyfills installed on a fresh browsing-context global. This asset is embedded in
// Broiler.HtmlBridge.Dom and evaluated once per document by DomBridge.RegisterContentRenderingPolyfills
// (Phase 3 work item 6 — externalized from inline C# string literals). Behaviour must stay identical to
// the former inline blocks; the C#-interop polyfills (document.cookie, crypto, DOMException, Node,
// SVGLength constructors) remain in Polyfills.cs because they are host-driven, not pure JS.

// Image() constructor — returns stub object with src property
function Image(width, height) {
    this.src = '';
    this.width = width || 0;
    this.height = height || 0;
    this.alt = '';
    this.complete = false;
    this.naturalWidth = 0;
    this.naturalHeight = 0;
    this.onload = null;
    this.onerror = null;
    this.addEventListener = function() {};
    this.removeEventListener = function() {};
}

// IntersectionObserver — stub that immediately invokes callback
function IntersectionObserver(callback, options) {
    this._callback = callback;
    this._targets = [];
}
IntersectionObserver.prototype.observe = function(target) {
    this._targets.push(target);
    // Immediately report as intersecting
    var entry = {
        target: target,
        isIntersecting: true,
        intersectionRatio: 1.0,
        boundingClientRect: { top: 0, left: 0, bottom: 0, right: 0, width: 0, height: 0 },
        intersectionRect: { top: 0, left: 0, bottom: 0, right: 0, width: 0, height: 0 },
        rootBounds: null,
        time: 0
    };
    try { this._callback([entry], this); } catch(e) {}
};
IntersectionObserver.prototype.unobserve = function(target) {
    this._targets = this._targets.filter(function(t) { return t !== target; });
};
IntersectionObserver.prototype.disconnect = function() {
    this._targets = [];
};
IntersectionObserver.prototype.takeRecords = function() {
    return [];
};

// ResizeObserver — no-op stub
function ResizeObserver(callback) {
    this._callback = callback;
}
ResizeObserver.prototype.observe = function() {};
ResizeObserver.prototype.unobserve = function() {};
ResizeObserver.prototype.disconnect = function() {};

// TextEncoder / TextDecoder — basic UTF-8 stubs
function TextEncoder() {
    this.encoding = 'utf-8';
}
TextEncoder.prototype.encode = function(str) {
    str = str || '';
    var arr = [];
    for (var i = 0; i < str.length; i++) {
        var c = str.charCodeAt(i);
        if (c < 0x80) {
            arr.push(c);
        } else if (c < 0x800) {
            arr.push(0xC0 | (c >> 6));
            arr.push(0x80 | (c & 0x3F));
        } else if (c >= 0xD800 && c <= 0xDBFF && i + 1 < str.length) {
            var next = str.charCodeAt(i + 1);
            if (next >= 0xDC00 && next <= 0xDFFF) {
                var cp = ((c - 0xD800) << 10) + (next - 0xDC00) + 0x10000;
                arr.push(0xF0 | (cp >> 18));
                arr.push(0x80 | ((cp >> 12) & 0x3F));
                arr.push(0x80 | ((cp >> 6) & 0x3F));
                arr.push(0x80 | (cp & 0x3F));
                i++;
            } else {
                arr.push(0xEF); arr.push(0xBF); arr.push(0xBD);
            }
        } else {
            arr.push(0xE0 | (c >> 12));
            arr.push(0x80 | ((c >> 6) & 0x3F));
            arr.push(0x80 | (c & 0x3F));
        }
    }
    return new Uint8Array(arr);
};
TextEncoder.prototype.encodeInto = function(str, dest) {
    var encoded = this.encode(str);
    var len = Math.min(encoded.length, dest.length);
    for (var i = 0; i < len; i++) dest[i] = encoded[i];
    return { read: str.length, written: len };
};

function TextDecoder(encoding) {
    this.encoding = (encoding || 'utf-8').toLowerCase();
    this.fatal = false;
    this.ignoreBOM = false;
}
TextDecoder.prototype.decode = function(input) {
    if (!input || input.length === 0) return '';
    var bytes = input instanceof Uint8Array ? input : new Uint8Array(input);
    var result = '';
    var len = bytes.length;
    for (var i = 0; i < len; ) {
        var b = bytes[i];
        if (b < 0x80) {
            result += String.fromCharCode(b);
            i++;
        } else if ((b & 0xE0) === 0xC0 && i + 1 < len) {
            result += String.fromCharCode(((b & 0x1F) << 6) | (bytes[i+1] & 0x3F));
            i += 2;
        } else if ((b & 0xF0) === 0xE0 && i + 2 < len) {
            result += String.fromCharCode(((b & 0x0F) << 12) | ((bytes[i+1] & 0x3F) << 6) | (bytes[i+2] & 0x3F));
            i += 3;
        } else if ((b & 0xF8) === 0xF0 && i + 3 < len) {
            var cp = ((b & 0x07) << 18) | ((bytes[i+1] & 0x3F) << 12) | ((bytes[i+2] & 0x3F) << 6) | (bytes[i+3] & 0x3F);
            cp -= 0x10000;
            result += String.fromCharCode(0xD800 + (cp >> 10), 0xDC00 + (cp & 0x3FF));
            i += 4;
        } else {
            result += '\uFFFD';
            i++;
        }
    }
    return result;
};

// URL / URLSearchParams polyfills
function URLSearchParams(init) {
    this._params = [];
    if (typeof init === 'string') {
        var s = init.charAt(0) === '?' ? init.substring(1) : init;
        var pairs = s.split('&');
        for (var i = 0; i < pairs.length; i++) {
            var kv = pairs[i].split('=');
            if (kv[0]) this._params.push([decodeURIComponent(kv[0]), decodeURIComponent(kv[1] || '')]);
        }
    } else if (init && typeof init === 'object') {
        var keys = Object.keys(init);
        for (var j = 0; j < keys.length; j++) {
            this._params.push([keys[j], String(init[keys[j]])]);
        }
    }
}
URLSearchParams.prototype.get = function(name) {
    for (var i = 0; i < this._params.length; i++) {
        if (this._params[i][0] === name) return this._params[i][1];
    }
    return null;
};
URLSearchParams.prototype.getAll = function(name) {
    var r = [];
    for (var i = 0; i < this._params.length; i++) {
        if (this._params[i][0] === name) r.push(this._params[i][1]);
    }
    return r;
};
URLSearchParams.prototype.has = function(name) { return this.get(name) !== null; };
URLSearchParams.prototype.set = function(name, value) {
    var found = false;
    for (var i = 0; i < this._params.length; i++) {
        if (this._params[i][0] === name) {
            if (!found) { this._params[i][1] = String(value); found = true; }
            else { this._params.splice(i, 1); i--; }
        }
    }
    if (!found) this._params.push([name, String(value)]);
};
URLSearchParams.prototype.append = function(name, value) {
    this._params.push([name, String(value)]);
};
URLSearchParams.prototype['delete'] = function(name) {
    this._params = this._params.filter(function(p) { return p[0] !== name; });
};
URLSearchParams.prototype.toString = function() {
    return this._params.map(function(p) {
        return encodeURIComponent(p[0]) + '=' + encodeURIComponent(p[1]);
    }).join('&');
};
URLSearchParams.prototype.forEach = function(cb) {
    for (var i = 0; i < this._params.length; i++) cb(this._params[i][1], this._params[i][0], this);
};

function URL(url, base) {
    if (base) {
        if (url.indexOf('://') === -1 && url.charAt(0) !== '/') {
            var baseNoQuery = base.split('?')[0].split('#')[0];
            var lastSlash = baseNoQuery.lastIndexOf('/');
            url = baseNoQuery.substring(0, lastSlash + 1) + url;
        } else if (url.charAt(0) === '/') {
            var m = base.match(/^([a-zA-Z][a-zA-Z0-9+\-.]*:\/\/[^\/]+)/);
            url = (m ? m[1] : '') + url;
        }
    }
    var match = url.match(/^([a-zA-Z][a-zA-Z0-9+\-.]*):\/\/([^\/:]+)(:\d+)?(\/[^?#]*)?(\?[^#]*)?(#.*)?$/);
    if (match) {
        this.protocol = match[1] + ':';
        this.hostname = match[2];
        this.port = match[3] ? match[3].substring(1) : '';
        this.host = this.hostname + (this.port ? ':' + this.port : '');
        this.pathname = match[4] || '/';
        this.search = match[5] || '';
        this.hash = match[6] || '';
        this.origin = this.protocol + '//' + this.host;
        this.href = url;
    } else {
        this.href = url;
        this.protocol = ''; this.hostname = ''; this.port = '';
        this.host = ''; this.pathname = url; this.search = '';
        this.hash = ''; this.origin = '';
    }
    this.searchParams = new URLSearchParams(this.search);
}
URL.prototype.toString = function() { return this.href; };
URL.prototype.toJSON = function() { return this.href; };

// AbortController / AbortSignal — basic stubs
function AbortController() {
    this.signal = {
        aborted: false,
        reason: undefined,
        onabort: null,
        _listeners: [],
        addEventListener: function(type, listener) {
            if (type !== 'abort' || typeof listener !== 'function') return;
            if (this._listeners.indexOf(listener) === -1) this._listeners.push(listener);
        },
        removeEventListener: function(type, listener) {
            if (type !== 'abort') return;
            var index = this._listeners.indexOf(listener);
            if (index !== -1) this._listeners.splice(index, 1);
        },
        throwIfAborted: function() {
            if (this.aborted) throw (this.reason !== undefined ? this.reason : new DOMException('The operation was aborted.', 'AbortError'));
        }
    };
}
AbortController.prototype.abort = function(reason) {
    if (this.signal.aborted) return;
    this.signal.aborted = true;
    this.signal.reason = reason !== undefined ? reason : new DOMException('The operation was aborted.', 'AbortError');
    var event = { type: 'abort', target: this.signal, currentTarget: this.signal };
    if (typeof this.signal.onabort === 'function') {
        try { this.signal.onabort(event); } catch(e) {}
    }
    var listeners = this.signal._listeners.slice();
    for (var i = 0; i < listeners.length; i++) {
        try { listeners[i].call(this.signal, event); } catch(e) {}
    }
};
