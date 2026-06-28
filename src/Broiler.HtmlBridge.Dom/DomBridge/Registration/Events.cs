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
    private void RegisterDocumentEventsAndMutationObservers(JSContext context)
    {
        // Event / typed event constructors — DOM Level 4
        context.Eval(@"
                function Event(type, options) {
                    options = options || {};
                    var evt = document.createEvent('Event');
                    evt.initEvent(type, options.bubbles === true, options.cancelable === true);
                    return evt;
                }

                function CustomEvent(type, options) {
                    options = options || {};
                    var evt = document.createEvent('CustomEvent');
                    evt.initCustomEvent(
                        type,
                        options.bubbles === true,
                        options.cancelable === true,
                        options.detail !== undefined ? options.detail : null);
                    return evt;
                }

                function MouseEvent(type, options) {
                    options = options || {};
                    var evt = document.createEvent('MouseEvents');
                    evt.initMouseEvent(
                        type,
                        options.bubbles === true,
                        options.cancelable === true,
                        options.view !== undefined ? options.view : null,
                        options.detail !== undefined ? options.detail : 0,
                        options.screenX !== undefined ? options.screenX : 0,
                        options.screenY !== undefined ? options.screenY : 0,
                        options.clientX !== undefined ? options.clientX : 0,
                        options.clientY !== undefined ? options.clientY : 0,
                        options.ctrlKey === true,
                        options.altKey === true,
                        options.shiftKey === true,
                        options.metaKey === true,
                        options.button !== undefined ? options.button : 0,
                        options.relatedTarget !== undefined ? options.relatedTarget : null);
                    return evt;
                }

                function FocusEvent(type, options) {
                    options = options || {};
                    var evt = document.createEvent('FocusEvents');
                    evt.initFocusEvent(
                        type,
                        options.bubbles === true,
                        options.cancelable === true,
                        options.view !== undefined ? options.view : null,
                        options.detail !== undefined ? options.detail : 0,
                        options.relatedTarget !== undefined ? options.relatedTarget : null);
                    return evt;
                }

                function KeyboardEvent(type, options) {
                    options = options || {};
                    var evt = document.createEvent('KeyboardEvents');
                    evt.initKeyboardEvent(
                        type,
                        options.bubbles === true,
                        options.cancelable === true,
                        options.view !== undefined ? options.view : null,
                        options.key !== undefined ? options.key : '',
                        options.location !== undefined ? options.location : 0,
                        options.ctrlKey === true,
                        options.altKey === true,
                        options.shiftKey === true,
                        options.metaKey === true,
                        options.repeat === true,
                        options.keyCode !== undefined ? options.keyCode : 0,
                        options.charCode !== undefined ? options.charCode : 0);
                    return evt;
                }

                function WheelEvent(type, options) {
                    options = options || {};
                    var evt = document.createEvent('WheelEvents');
                    var modifiers = [];
                    if (options.ctrlKey === true) modifiers.push('Control');
                    if (options.altKey === true) modifiers.push('Alt');
                    if (options.shiftKey === true) modifiers.push('Shift');
                    if (options.metaKey === true) modifiers.push('Meta');
                    evt.initWheelEvent(
                        type,
                        options.bubbles === true,
                        options.cancelable === true,
                        options.view !== undefined ? options.view : null,
                        options.detail !== undefined ? options.detail : 0,
                        options.screenX !== undefined ? options.screenX : 0,
                        options.screenY !== undefined ? options.screenY : 0,
                        options.clientX !== undefined ? options.clientX : 0,
                        options.clientY !== undefined ? options.clientY : 0,
                        options.button !== undefined ? options.button : 0,
                        options.relatedTarget !== undefined ? options.relatedTarget : null,
                        modifiers.join(' '),
                        options.deltaX !== undefined ? options.deltaX : 0,
                        options.deltaY !== undefined ? options.deltaY : 0,
                        options.deltaZ !== undefined ? options.deltaZ : 0,
                        options.deltaMode !== undefined ? options.deltaMode : 0);
                    return evt;
                }

                function UIEvent(type, options) {
                    options = options || {};
                    var evt = document.createEvent('UIEvents');
                    evt.initUIEvent(
                        type,
                        options.bubbles === true,
                        options.cancelable === true,
                        options.view !== undefined ? options.view : null,
                        options.detail !== undefined ? options.detail : 0);
                    return evt;
                }

                function InputEvent(type, options) {
                    options = options || {};
                    var evt = document.createEvent('InputEvent');
                    evt.initInputEvent(
                        type,
                        options.bubbles === true,
                        options.cancelable === true,
                        options.view !== undefined ? options.view : null,
                        options.data !== undefined ? options.data : null,
                        options.inputType !== undefined ? options.inputType : '',
                        options.isComposing === true);
                    return evt;
                }
            ");
        var registerMutationObserverFn = new JSFunction(JsRegistrationBroilerRegisterMutationObserver034Core, "__broilerRegisterMutationObserver", 3);
        var unregisterMutationObserverFn = new JSFunction(JsRegistrationBroilerUnregisterMutationObserver035Core, "__broilerUnregisterMutationObserver", 1);
        context["__broilerRegisterMutationObserver"] = registerMutationObserverFn;
        context["__broilerUnregisterMutationObserver"] = unregisterMutationObserverFn;
        // MutationObserver — DOM Level 4
        context.Eval(@"
                function MutationObserver(callback) {
                    this._callback = callback;
                    this._targets = [];
                    this._records = [];
                }
                MutationObserver.prototype.observe = function(target, options) {
                    var normalizedOptions = options || {};
                    this._targets.push({ target: target, options: normalizedOptions });
                    if (typeof __broilerRegisterMutationObserver === 'function') {
                        __broilerRegisterMutationObserver(this, target, normalizedOptions);
                    }
                };
                MutationObserver.prototype.disconnect = function() {
                    this._targets = [];
                    this._records = [];
                    if (typeof __broilerUnregisterMutationObserver === 'function') {
                        __broilerUnregisterMutationObserver(this);
                    }
                };
                MutationObserver.prototype.takeRecords = function() {
                    var r = this._records.slice();
                    this._records = [];
                    return r;
                };
                MutationObserver.prototype._notify = function(records) {
                    if (records && records.length > 0) {
                        for (var i = 0; i < records.length; i++) {
                            this._records.push(records[i]);
                        }
                        var pending = this._records.slice();
                        this._records = [];
                        try { this._callback(pending, this); } catch(e) {}
                    }
                };
            ");
    }

}
