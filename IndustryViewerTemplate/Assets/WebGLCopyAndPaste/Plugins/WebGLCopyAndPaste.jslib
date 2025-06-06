/*
 * Copyright 2020, Gregg Tavares.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are
 * met:
 *
 *     * Redistributions of source code must retain the above copyright
 * notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above
 * copyright notice, this list of conditions and the following disclaimer
 * in the documentation and/or other materials provided with the
 * distribution.
 *     * Neither the name of Gregg Tavares. nor the names of its
 * contributors may be used to endorse or promote products derived from
 * this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
 * OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
var WebGLCopyAndPaste = {
  $WebGLCopyAndPaste: {},

  initWebGLCopyAndPaste__postset: '_initWebGLCopyAndPaste();',

  initWebGLCopyAndPaste: function () {
    // for some reason only on Safari does Unity call
    // preventDefault so let's prevent preventDefault
    // so the browser will generate copy and paste events
    window.addEventListener = function (origFn) {
      function noop() {
      }

      // I hope c,x,v are universal
      const keys = {'c': true, 'x': true, 'v': true};

      // Emscripten doesn't support the spread operator or at
      // least the one used by Unity 2019.4.1
      return function (name, fn) {
        const args = Array.prototype.slice.call(arguments);
        if (name !== 'keypress') {
          return origFn.apply(this, args);
        }
        args[1] = function (event) {
          const hArgs = Array.prototype.slice.call(arguments);
          if (keys[event.key.toLowerCase()] &&
              ((event.metaKey ? 1 : 0) + (event.ctrlKey ? 1 : 0)) === 1) {
            event.preventDefault = noop;
          }
          return fn.apply(this, hArgs);
        };
        return origFn.apply(this, args);
      };
    }(window.addEventListener);

    _initWebGLCopyAndPaste = function (cutCopyFuncPtr, pasteFuncPtr) {

      function sendStringCallback (callback, str) {
        var bufferSize = lengthBytesUTF8(str) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(str, buffer, bufferSize);
        if (typeof Module !== "undefined" && typeof Module.dynCall_vi !== "undefined") {
          Module.dynCall_vi(callback, buffer);
        } else if (typeof Runtime !== "undefined" && typeof Runtime.dynCall !== "undefined") {
          Runtime.dynCall('vi', callback, [buffer]);
        } else if (typeof getWasmTableEntry !== "undefined") {
          getWasmTableEntry(callback)(buffer);
        } else {
          throw new Error("[WebGLCopyAndPaste]: Couldn't find 'Module.dynCall_vi', 'Runtime.dynCall' or 'getWasmTableEntry'");
        }
      }

      WebGLCopyAndPaste.data =
          WebGLCopyAndPaste.data || {
            initialized: false,
            cutCopyFunc: cutCopyFuncPtr,
            pasteFunc: pasteFuncPtr,
          };
      const g = WebGLCopyAndPaste.data;

      if (!g.initialized) {
        window.addEventListener('cut', function (e) {
          e.preventDefault();
          sendStringCallback(g.cutCopyFunc, 'x');
          e.clipboardData.setData('text/plain', g.clipboardStr);
        });
        window.addEventListener('copy', function (e) {
          e.preventDefault();
          sendStringCallback(g.cutCopyFunc, 'c');
          e.clipboardData.setData('text/plain', g.clipboardStr);
        });
        window.addEventListener('paste', function (e) {
          const str = e.clipboardData.getData('text');
          sendStringCallback(g.pasteFunc, str);
        });
      }
    };
  },

  
  passCopyToBrowser: function (stringPtr) {
    var fn = typeof UTF8ToString === 'function' ? UTF8ToString : Pointer_stringify;
    WebGLCopyAndPaste.data.clipboardStr = fn(stringPtr);
  },


  copyTextToClipboard: function (stringPtr) {
    var fn = typeof UTF8ToString === 'function' ? UTF8ToString : Pointer_stringify;
    var textToCopy = fn(stringPtr);

    tryCopyWithClipboardAPI(() => tryCopyWithExecCommand(() => console.error("[WebGLCopyAndPaste]: Couldn't find a way to copy text manually")));

    // Tries to use "navigator.clipboard" if it exists and it's a secure context,
    // as it doesn't work if we are not in "https" pages.
    function tryCopyWithClipboardAPI(onFailure) {
      if (navigator.clipboard === undefined || !window.isSecureContext) {
        onFailure();
        return;
      }

      navigator.clipboard.writeText(textToCopy)
      .catch(error => onFailure());
    }

    // Tries to use "document.execCommand('copy')", which is obsolete but usually still works.
    // We create a text area to copy the text to and select from it, then we remove it.
    function tryCopyWithExecCommand(onFailure) {
      if (document.execCommand === undefined) {
        onFailure();
        return;
      }

      const prevActiveElement = document.activeElement;
      
      const textArea = document.createElement("textarea");
      textArea.value = textToCopy;
      textArea.style.position = "fixed";
      textArea.style.width = 1;
      textArea.style.height = 1;
      textArea.style.opacity = 0;
      document.body.appendChild(textArea);
      
      // Trying to copy immediately after selecting doesn't seem to work on all browsers,
      // so we do it from the "onselect" event, which seems to work fine everywhere.
      textArea.onselect = () => {
        try {
          document.execCommand("copy");
        } catch (error) {
          onFailure();
        }
        finally {
          prevActiveElement.focus({ preventScroll: true });
          textArea.remove();
        }
      };
      
      textArea.focus({ preventScroll: true });
      textArea.select();
    }
  },


  copyTextToClipboardWithExecCommand: function (text) {
  },
};

autoAddDeps(WebGLCopyAndPaste, '$WebGLCopyAndPaste');
mergeInto(LibraryManager.library, WebGLCopyAndPaste);
