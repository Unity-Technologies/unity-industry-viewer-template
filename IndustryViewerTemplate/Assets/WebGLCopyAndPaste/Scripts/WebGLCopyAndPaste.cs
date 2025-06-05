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

#if UNITY_WEBGL

// To add support for TextMesh Pro input fields,
// uncomment the following line or add its symbol to the build symbols:
// #define WEBGL_COPY_AND_PASTE_SUPPORT_TEXTMESH_PRO

using UnityEngine;
using UnityEngine.EventSystems;
using System.Runtime.InteropServices;
using UnityEngine.Scripting;

namespace WebGLCopyAndPaste
{
    /// <summary>
    /// (Use only if "UNITY_WEBGL" is defined)
    /// Provides copy & paste functionality in WebGL builds.
    /// </summary>

    [Preserve]
    public static class WebGLCopyAndPasteAPI
    {
        #region Types


        delegate void StringCallback(string text);


        #endregion




        #region JavaScript methods


        [DllImport("__Internal")]
        private static extern void initWebGLCopyAndPaste(StringCallback cutCopyCallback, StringCallback pasteCallback);

        [DllImport("__Internal")]
        private static extern void passCopyToBrowser(string text);

        [DllImport("__Internal")]
        private static extern void copyTextToClipboard(string text);


        #endregion




        /// <summary>
        /// Performs initialization at runtime.
        /// </summary>

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Initialize()
        {
            if (!Application.isEditor)
            {
                initWebGLCopyAndPaste(OnCutOrCopyRequested, OnPasteRequested);
            }
        }




        /// <summary>
        /// Copies the specified text to the clipboard.
        /// </summary>
        /// <param name="text">The text to copy.</param>

        public static void CopyToClipboard(string text)
        {
            //TrisiboTODO: Add a way to know if it succeeded.

            if (!Application.isEditor)
            {
                copyTextToClipboard(text);
            }

            // This is needed even in a build to ensure "systemCopyBuffer" contains the copied text:
            GUIUtility.systemCopyBuffer = text;
        }




        /// <summary>
        /// Creates a keyboard event with the specified key plus the "Control" and "Command" keys pressed.
        /// </summary>
        /// <param name="baseKey">The key to be pressed for the event.</param>
        /// <returns>The event.</returns>

        private static Event CreateKeyboardEventWithControlAndCommandModifiers(string baseKey)
        {
            var keyboardEvent = Event.KeyboardEvent(baseKey);
            keyboardEvent.control = true;
            keyboardEvent.command = true;
            return keyboardEvent;
        }




        /// <summary>
        /// Sends and event with the specified key plus the "Control" and "Command" keys pressed
        /// to the input field of the currently selected <see cref="GameObject"/>, if any.
        /// </summary>
        /// <param name="baseKey">The key to send.</param>
        /// <param name="forceLabelUpdate">Whether to force the label of the input field to update.</param>

        private static void SendKeyboardEventWithControlAndCommandModifiersToSelectedInputField(string baseKey, bool forceLabelUpdate = false)
        {
            var currentEventSystem = EventSystem.current;
            if (currentEventSystem == null)
            {
                return;
            }

            var currentObj = currentEventSystem.currentSelectedGameObject;
            if (currentObj == null)
            {
                return;
            }

            #if WEBGL_COPY_AND_PASTE_SUPPORT_TEXTMESH_PRO
            if (currentObj.TryGetComponent<TMPro.TMP_InputField>(out var tmproInputField))
            {
                tmproInputField.ProcessEvent(CreateKeyboardEventWithControlAndCommandModifiers(baseKey));
                if (forceLabelUpdate)
                {
                    tmproInputField.ForceLabelUpdate();
                }
                return;
            }
            #endif

            if (currentObj.TryGetComponent<UnityEngine.UI.InputField>(out var legacyInputField))
            {
                legacyInputField.ProcessEvent(CreateKeyboardEventWithControlAndCommandModifiers(baseKey));
                if (forceLabelUpdate)
                {
                    legacyInputField.ForceLabelUpdate();
                }
                return;
            }
        }




        /// <summary>
        /// Called when the user requested to cut or copy.
        /// </summary>
        /// <param name="key">The key the user used to cut or copy.</param>

        [AOT.MonoPInvokeCallback(typeof(StringCallback))]
        private static void OnCutOrCopyRequested(string key)
        {
            SendKeyboardEventWithControlAndCommandModifiersToSelectedInputField(key);
            passCopyToBrowser(GUIUtility.systemCopyBuffer);
        }




        /// <summary>
        /// Called when the user requested to paste.
        /// </summary>
        /// <param name="text">The pasted text.</param>

        [AOT.MonoPInvokeCallback(typeof(StringCallback))]
        private static void OnPasteRequested(string text)
        {
            // Assigning the text to "GUIUtility.systemCopyBuffer" causes it to be automatically pasted on some browsers on the next frame,
            // but not on all (e.g. Firefox 120.0.1, Windows 10, Unity 2022.3.10).
            // Using "SendKeyboardEventToSelectedInputField" with the "v" key properly pastes the text on all tested browsers (in the current frame),
            // but it needs "GUIUtility.systemCopyBuffer" to be set,
            // and doing so would paste the text twice on browsers in which setting "GUIUtility.systemCopyBuffer" works.
            // As a workaround, we set "GUIUtility.systemCopyBuffer", then call "SendKeyboardEventToSelectedInputField",
            // and then set "GUIUtility.systemCopyBuffer" to null;
            // this prevents the paste that occurs on the next frame, and only the "SendKeyboardEventToSelectedInputField" one is made.
            // Confirmed to work on:
            //   - Edge 120.0.2210.61 (Chromium) on Windows 10, Unity 2022.3.10, 2021.3.25 and 2020.3.18.
            //   - Firefox 120.0.1 on Windows 10, Unity 2022.3.10, 2021.3.25 and 2020.3.18.
            //   - Safari 16.6 on macOS Ventura 13.6, Unity 2022.3.10.
            //   - Chrome 118.0.5993.70 on macOS Ventura 13.6, Unity 2022.3.10.
            //   - Firefox 120.0.1 on macOS Ventura 13.6, Unity 2022.3.10.
            GUIUtility.systemCopyBuffer = text;
            SendKeyboardEventWithControlAndCommandModifiersToSelectedInputField("v", true);
            GUIUtility.systemCopyBuffer = null;
        }

    }
}

#endif  // UNITY_WEBGL
