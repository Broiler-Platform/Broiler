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

// AbortController / AbortSignal (DOM §3.2).
//
// The signal used to be an object literal built inside the controller, with no AbortSignal
// constructor anywhere. Everything that touches a signal *through the controller* worked, so this
// looked complete — but the name itself did not exist, and a script that so much as mentions
// `AbortSignal` gets a ReferenceError, which aborts the whole script rather than the one line.
// That is what google.com's main bundle does, and it is where the bundle stopped once it began
// parsing at all: nothing it defines after that point came into existence.
//
// So AbortSignal is a real constructor with a real prototype. `aborted`, `reason` and `onabort`
// stay *own* properties of each signal, because the host reads them directly off the object; only
// the methods moved to the prototype, which is what makes `instanceof` and the static factories
// work.
(function () {
    function AbortSignal() {
        // The interface has no constructor of its own: a signal only ever comes from an
        // AbortController or from one of the statics below.
        throw new TypeError("Failed to construct 'AbortSignal': Illegal constructor.");
    }

    // A signal is an EventTarget, so inherit when there is one to inherit from — that is what
    // makes `signal instanceof EventTarget` true. Guarded rather than assumed: if EventTarget is
    // absent the plain prototype below still carries everything a signal is actually used for.
    if (typeof EventTarget === 'function' && EventTarget.prototype) {
        AbortSignal.prototype = Object.create(EventTarget.prototype);
        AbortSignal.prototype.constructor = AbortSignal;
    }

    function createSignal() {
        var signal = Object.create(AbortSignal.prototype);
        signal.aborted = false;
        signal.reason = undefined;
        signal.onabort = null;
        signal._listeners = [];
        return signal;
    }

    // The abort steps, shared by the controller and by AbortSignal.abort/timeout/any. Aborting an
    // already-aborted signal is a no-op, so a listener fires at most once however it was reached.
    function abortSignal(signal, reason) {
        if (signal.aborted) return;
        signal.aborted = true;
        signal.reason = reason !== undefined ? reason : new DOMException('The operation was aborted.', 'AbortError');
        var event = { type: 'abort', target: signal, currentTarget: signal };
        if (typeof signal.onabort === 'function') {
            try { signal.onabort(event); } catch (e) {}
        }
        var listeners = signal._listeners.slice();
        for (var i = 0; i < listeners.length; i++) {
            try { listeners[i].call(signal, event); } catch (e) {}
        }
    }

    AbortSignal.prototype.addEventListener = function (type, listener) {
        if (type !== 'abort' || typeof listener !== 'function') return;
        if (this._listeners.indexOf(listener) === -1) this._listeners.push(listener);
    };

    AbortSignal.prototype.removeEventListener = function (type, listener) {
        if (type !== 'abort') return;
        var index = this._listeners.indexOf(listener);
        if (index !== -1) this._listeners.splice(index, 1);
    };

    AbortSignal.prototype.throwIfAborted = function () {
        if (this.aborted) throw (this.reason !== undefined ? this.reason : new DOMException('The operation was aborted.', 'AbortError'));
    };

    // AbortSignal.abort(reason) — a signal that is already aborted.
    AbortSignal.abort = function (reason) {
        var signal = createSignal();
        abortSignal(signal, reason);
        return signal;
    };

    // AbortSignal.timeout(ms) — aborts with a TimeoutError, which is deliberately not an
    // AbortError: code that distinguishes "the user cancelled" from "it took too long" reads
    // reason.name to tell them apart.
    AbortSignal.timeout = function (milliseconds) {
        var signal = createSignal();
        setTimeout(function () {
            abortSignal(signal, new DOMException('The operation was aborted due to timeout.', 'TimeoutError'));
        }, milliseconds);
        return signal;
    };

    // AbortSignal.any(signals) — follows whichever aborts first, and is already aborted if any of
    // them is, so a caller cannot miss an abort that happened before it composed them.
    AbortSignal.any = function (signals) {
        var composite = createSignal();
        var sources = signals ? Array.prototype.slice.call(signals) : [];
        for (var i = 0; i < sources.length; i++) {
            if (sources[i] && sources[i].aborted) {
                abortSignal(composite, sources[i].reason);
                return composite;
            }
        }
        for (var j = 0; j < sources.length; j++) {
            (function (source) {
                if (!source || typeof source.addEventListener !== 'function') return;
                source.addEventListener('abort', function () { abortSignal(composite, source.reason); });
            })(sources[j]);
        }
        return composite;
    };

    function AbortController() {
        this.signal = createSignal();
    }

    AbortController.prototype.abort = function (reason) {
        abortSignal(this.signal, reason);
    };

    globalThis.AbortSignal = AbortSignal;
    globalThis.AbortController = AbortController;
})();

// CSS Font Loading (css-font-loading-3) — FontFace and the document.fonts FontFaceSet.
//
// The API did not exist, so `document.fonts` was undefined and `document.fonts.load(…)` a
// TypeError. That is not confined to the font code that asks for it: on google.com the very first
// inline script is a font preloader whose whole body is one `document.fonts.load` loop, so the
// script dies on its first statement and everything it would have gone on to do never happens.
//
// Both halves ship together deliberately. A FontFaceSet with no FontFace constructor is the shape
// that hid the AbortSignal gap for so long — `document.fonts.add(new FontFace(…))` is the ordinary
// way to use this API, and it needs both names to exist.
//
// What it models: Broiler resolves fonts synchronously against what it already has when it lays
// text out, so from a page's point of view there is never a load in flight. `status` is therefore
// "loaded", `ready` is already resolved, and `check()` is true. `load()` resolves rather than
// rejecting — a page calling it is asking Broiler to *start* a load it has no pending work for,
// and the failure mode that matters is a promise that never settles, which would strand a page
// waiting behind `document.fonts.ready` before it renders anything.
(function () {
    function FontFace(family, source, descriptors) {
        this.family = family !== undefined ? String(family) : '';
        this.style = 'normal';
        this.weight = 'normal';
        this.stretch = 'normal';
        this.unicodeRange = 'U+0-10FFFF';
        this.variant = 'normal';
        this.featureSettings = 'normal';
        this.variationSettings = 'normal';
        this.display = 'auto';
        this.ascentOverride = 'normal';
        this.descentOverride = 'normal';
        this.lineGapOverride = 'normal';

        if (descriptors) {
            for (var key in descriptors) {
                if (Object.prototype.hasOwnProperty.call(descriptors, key)) this[key] = descriptors[key];
            }
        }

        // A FontFace built from a source is "unloaded" until load() is called; one whose source is
        // already binary data (an ArrayBuffer rather than a url()) is loaded on construction, per
        // the spec's split between the two constructor forms.
        var isBinarySource = source !== undefined && typeof source !== 'string';
        this.status = isBinarySource ? 'loaded' : 'unloaded';
        this._source = source;

        var self = this;
        this.loaded = isBinarySource
            ? Promise.resolve(self)
            : new Promise(function (resolve) { self._resolveLoaded = resolve; });
        if (isBinarySource) this._resolveLoaded = null;
    }

    FontFace.prototype.load = function () {
        if (this.status === 'unloaded' || this.status === 'loading') {
            this.status = 'loaded';
            if (this._resolveLoaded) { this._resolveLoaded(this); this._resolveLoaded = null; }
        }
        return this.loaded;
    };

    function FontFaceSet() {
        this._faces = [];
        this.onloading = null;
        this.onloadingdone = null;
        this.onloadingerror = null;
        this._listeners = {};
    }

    // Set-like, which is what iteration and `size` on document.fonts rely on.
    Object.defineProperty(FontFaceSet.prototype, 'size', {
        get: function () { return this._faces.length; },
        configurable: true
    });

    // Nothing is ever in flight, so the set is loaded and ready from the start. `ready` is cached
    // rather than rebuilt per access: a page may await it more than once, and each access handing
    // back a different promise is a subtle way to strand one of them.
    Object.defineProperty(FontFaceSet.prototype, 'status', {
        get: function () { return 'loaded'; },
        configurable: true
    });

    Object.defineProperty(FontFaceSet.prototype, 'ready', {
        get: function () {
            if (!this._ready) this._ready = Promise.resolve(this);
            return this._ready;
        },
        configurable: true
    });

    FontFaceSet.prototype.add = function (face) {
        if (face && this._faces.indexOf(face) === -1) this._faces.push(face);
        return this;
    };

    FontFaceSet.prototype.delete = function (face) {
        var index = this._faces.indexOf(face);
        if (index === -1) return false;
        this._faces.splice(index, 1);
        return true;
    };

    FontFaceSet.prototype.clear = function () { this._faces.length = 0; };
    FontFaceSet.prototype.has = function (face) { return this._faces.indexOf(face) !== -1; };

    FontFaceSet.prototype.forEach = function (callback, thisArg) {
        for (var i = 0; i < this._faces.length; i++) {
            callback.call(thisArg, this._faces[i], this._faces[i], this);
        }
    };

    FontFaceSet.prototype.values = function () { return this._faces.slice()[Symbol.iterator](); };
    FontFaceSet.prototype.keys = function () { return this.values(); };
    FontFaceSet.prototype.entries = function () {
        var pairs = [];
        for (var i = 0; i < this._faces.length; i++) pairs.push([this._faces[i], this._faces[i]]);
        return pairs[Symbol.iterator]();
    };
    if (typeof Symbol !== 'undefined' && Symbol.iterator) {
        FontFaceSet.prototype[Symbol.iterator] = function () { return this.values(); };
    }

    // load()/check() take a CSS `font` shorthand. An absent or empty one is a SyntaxError, as the
    // spec requires; beyond that Broiler does not parse the shorthand, so a malformed but non-empty
    // font resolves rather than rejecting. That is the deliberate direction: a page's font string
    // is rarely the thing it is testing, and rejecting one Broiler merely failed to parse would
    // break pages over a diagnostic Broiler cannot actually produce.
    function requireFontShorthand(font) {
        if (font === undefined || String(font).trim() === '') {
            throw new DOMException("Failed to parse the 'font' property.", 'SyntaxError');
        }
    }

    FontFaceSet.prototype.load = function (font, text) {
        try {
            requireFontShorthand(font);
        } catch (e) {
            return Promise.reject(e);
        }

        // Resolves with the faces this set holds for the family, which is the empty list unless the
        // page added FontFace objects itself — Broiler's own fonts are not modelled as FontFace
        // instances, so claiming them here would hand back objects that describe nothing.
        var matching = [];
        for (var i = 0; i < this._faces.length; i++) {
            var face = this._faces[i];
            if (face.status !== 'loaded') face.load();
            if (String(font).indexOf(face.family) !== -1) matching.push(face);
        }
        return Promise.resolve(matching);
    };

    FontFaceSet.prototype.check = function (font, text) {
        requireFontShorthand(font);
        // Text is laid out with whatever Broiler resolves the family to, so from the page's side
        // the font it asked about is always available to draw with.
        return true;
    };

    FontFaceSet.prototype.addEventListener = function (type, listener) {
        if (typeof listener !== 'function') return;
        if (!this._listeners[type]) this._listeners[type] = [];
        if (this._listeners[type].indexOf(listener) === -1) this._listeners[type].push(listener);
    };

    FontFaceSet.prototype.removeEventListener = function (type, listener) {
        var listeners = this._listeners[type];
        if (!listeners) return;
        var index = listeners.indexOf(listener);
        if (index !== -1) listeners.splice(index, 1);
    };

    globalThis.FontFace = FontFace;
    globalThis.FontFaceSet = FontFaceSet;

    if (typeof document !== 'undefined' && document && !document.fonts) {
        document.fonts = new FontFaceSet();
    }
})();
