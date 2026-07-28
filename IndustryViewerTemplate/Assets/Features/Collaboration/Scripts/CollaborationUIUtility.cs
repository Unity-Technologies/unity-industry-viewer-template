using System;
using System.Collections.Generic;
using System.IO;
using Unity.AppUI.UI;
using System.Linq;
using Unity.Cloud.Collaboration;
using Unity.Industry.Viewer.Shared;
using UnityEngine.UIElements;
using Unity.AppUI.Core;
using System.Text.RegularExpressions;
using UnityEngine;
using Unity.Cloud.Common;
using Unity.Industry.Viewer.Assets;
using Unity.Cloud.Identity;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Unity.Industry.Viewer.Collaboration
{
    public enum AddAttachmentFailType
    {
        None,
        DuplicateFilePath,
        DuplicateFileName
    }
    
    public static class CollaborationUIUtility
    {
        private static readonly string[] Colors = new string[]
        {
            "#E54D2E", // tomatoDark.tomato9
            "#00A2C7", // cyanDark.cyan9
            "#F76B15", // orangeDark.orange9
            "#8E4EC6", // violetDark.violet9
            "#12A594", // tealDark.teal9
            "#46A758", // greenDark.green9
            "#AB4ABA"  // plumDark.plum9
        };
        
        public static Dictionary<string, string> ReactionData = new Dictionary<string, string>()
        {
            { "thumbup", "👍" },
            { "thumbdown", "👎" },
            { "celebrate", "🎉" },
            { "look" , "👀" },
            { "hundred", "💯" },
            { "check", "✅" },
            { "fire", "🔥" },
            { "smiley", "😀" }
        };

        public static Popover NamePopover;
        
        private static Dictionary<OrganizationId, IOrganization> m_AllOrganizations;
        private static Dictionary<OrganizationId, List<IUserInfo>> m_OrganizationMembers;
        private static bool shouldIgnoreKey;
        // In-flight member fetches, keyed per org so callers share one task (single-flight).
        private static Dictionary<OrganizationId, Task> m_MemberFetches;

        public static bool JustDismissedPopover
        {
            get => _justDismissed;
            set
            {
                _justDismissed = value;
                if (_justDismissed)
                {
                    // Reset after a short delay to allow immediate re-triggering
                    Task.Delay(250).ContinueWith(_ => _justDismissed = false);
                }
            }
        }
        private static bool _justDismissed;

        // One tracker per composer TextArea; entries drop out automatically when the UI element is collected.
        private static readonly ConditionalWeakTable<TextArea, MentionSpanTracker> s_MentionTrackers = new();

        public static MentionSpanTracker GetTracker(TextArea textArea) => s_MentionTrackers.GetOrCreateValue(textArea);

        public static void ClearMentions(TextArea textArea)
        {
            if (textArea == null) return;
            GetTracker(textArea).Clear();
            RefreshMentionMirror(textArea, textArea.value);
            if (ReferenceEquals(s_ActiveTypeaheadComposer, textArea))
            {
                s_ActiveTypeaheadComposer = null;
            }
            if (s_InputBindings.TryGetValue(textArea, out var binding))
            {
                binding.PendingAutoSpace = -1;
                // A reset also invalidates any deferred work still in flight for this composer.
                binding.PendingCommit?.Pause();
                binding.TypeaheadRecovery?.Pause();
            }
        }

        // Matches :user[name]{#id} — group 1 = display name, group 2 = user id. The EMIT
        // side of this format lives in MentionSpanTracker.ToCloudFormat; keep them in sync.
        private const string k_CloudMentionPattern = @":user\[([^\]]+)\]\{#([^}]+)\}";

        // The wire format cannot represent ']' in a display name (the pattern above
        // terminates the name at the first ']'), and an unescaped ']' would let a crafted
        // display name forge a mention of another user's id. Brackets are swapped for
        // parentheses at span creation, so the visible text, the span, and the emitted
        // wire text always agree and always round-trip.
        internal static string SanitizeMentionName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return name.Replace('[', '(').Replace(']', ')');
        }

        /// <summary>
        /// Returns the composer text in the cloud wire format, with confirmed mentions
        /// converted to :user[name]{#id}.
        /// </summary>
        public static string GetCloudText(TextArea textArea)
        {
            // Convert from the INNER field's buffer: the span pipeline tracks it, and it
            // can be ahead of the TextArea's cached value when an on-screen keyboard
            // commits text that only the poll observed.
            var textField = textArea.Q<UnityEngine.UIElements.TextField>();
            return GetTracker(textArea).ToCloudFormat(textField != null ? textField.value : textArea.value);
        }

        /// <summary>
        /// Loads stored cloud-format text into a composer for editing: each :user[name]{#id}
        /// becomes plain "@Name" text backed by a mention span, ready for re-conversion by
        /// <see cref="GetCloudText"/> at save time.
        /// </summary>
        public static void LoadCloudTextIntoComposer(TextArea textArea, string cloudText)
        {
            var tracker = GetTracker(textArea);
            tracker.Clear();

            string plainText = cloudText ?? string.Empty;
            if (!string.IsNullOrEmpty(plainText))
            {
                var matches = Regex.Matches(plainText, k_CloudMentionPattern);
                if (matches.Count > 0)
                {
                    var builder = new StringBuilder(plainText.Length);
                    int cursor = 0;
                    foreach (Match match in matches)
                    {
                        builder.Append(plainText, cursor, match.Index - cursor);
                        var name = match.Groups[1].Value;
                        var visible = "@" + name;
                        tracker.AddSpan(builder.Length, visible.Length, match.Groups[2].Value, name);
                        builder.Append(visible);
                        cursor = match.Index + match.Length;
                    }
                    builder.Append(plainText, cursor, plainText.Length - cursor);
                    plainText = builder.ToString();
                }
            }

            CommitProgrammaticText(textArea, tracker, plainText);
        }

        // Owns the tail of every programmatic composer-text mutation: the input-dedupe
        // snapshot, the silent value write, and the mirror repaint must always move
        // together — a site that skips the snapshot has its own write re-processed as a
        // user edit (dissolving fresh spans); one that skips the repaint leaves stale or
        // invisible text (the editable ink is transparent once the mirror owns rendering).
        private static void CommitProgrammaticText(TextArea textArea, MentionSpanTracker tracker, string newText)
        {
            tracker.LastObservedText = newText;
            textArea.SetValueWithoutNotify(newText);
            RefreshMentionMirror(textArea, newText);
        }

        // Display color for mentions, shared by rendered comments (ParseUserTags) and the
        // composer highlight mirror.
        private const string k_MentionColorHex = "#0070CA";

        // The composer highlight layer: a read-only TextElement overlaid on the editable
        // text that renders ALL of the text — plain runs in the captured theme color and
        // mention runs in blue — while the editable element's own ink is made transparent.
        // Every glyph is drawn exactly once (painting blue OVER the editable glyphs left a
        // visible anti-aliasing rim on high-DPI displays). The caret and the selection
        // highlight are drawn by separate painters with their own theme colors, so hiding
        // the ink does not affect them. All inserted rich-text tags are zero-width, so the
        // mirror's line wrapping stays glyph-identical to the editable buffer. Until the
        // theme text color has been captured (first laid-out sync), the editable ink stays
        // visible and the mirror stays empty, so text is never invisible.
        private class MentionMirror
        {
            public TextElement Element;
            public TextElement Source;
            public ScrollView InnerScroll; // The TextField's internal scroller, if any.
            public string TextColorHex; // "#RRGGBBAA"; null until captured.
        }

        private static readonly ConditionalWeakTable<TextArea, MentionMirror> s_MentionMirrors = new();

        public static void AttachMentionMirror(TextArea textArea)
        {
            if (textArea == null) return;
            var textField = textArea.Q<UnityEngine.UIElements.TextField>();
            var source = textField?.Q<TextElement>();
            if (source == null || textField.parent == null) return;

            // Idempotent for repeated InitializeUI calls on the same composer.
            if (s_MentionMirrors.TryGetValue(textArea, out var existing) && existing.Element?.parent == textField.parent) return;

            var mirrorElement = new TextElement
            {
                pickingMode = PickingMode.Ignore,
                focusable = false,
                enableRichText = true
            };
            mirrorElement.style.position = Position.Absolute;
            // Base color must stay OPAQUE: UI Toolkit combines it with the per-run rich-text
            // colors, so a transparent base would wipe out the colored runs. White is only a
            // placeholder — the first laid-out sync replaces it with the captured theme
            // color, which the zero-mention fast path in RefreshMentionMirror renders directly.
            mirrorElement.style.color = Color.white;
            // The mirror is aligned to the source's CONTENT rect, so it must not add any
            // box-model offsets of its own (theme rules could give a bare TextElement some).
            mirrorElement.style.marginLeft = 0;
            mirrorElement.style.marginTop = 0;
            mirrorElement.style.marginRight = 0;
            mirrorElement.style.marginBottom = 0;
            mirrorElement.style.paddingLeft = 0;
            mirrorElement.style.paddingTop = 0;
            mirrorElement.style.paddingRight = 0;
            mirrorElement.style.paddingBottom = 0;

            // Re-attach: carry the previously captured theme color over (the editable ink
            // is already transparent, so re-capturing from the source would freeze
            // '#00000000' and render all plain text invisible) and retire the old overlay.
            if (existing?.TextColorHex != null && ColorUtility.TryParseHtmlString(existing.TextColorHex, out var carriedColor))
            {
                mirrorElement.style.color = carriedColor;
            }
            existing?.Element?.RemoveFromHierarchy();

            var innerScroll = textField.Q<ScrollView>();
            s_MentionMirrors.Remove(textArea);
            s_MentionMirrors.Add(textArea, new MentionMirror { Element = mirrorElement, Source = source, InnerScroll = innerScroll, TextColorHex = existing?.TextColorHex });

            // Sibling of the TextField inside the ScrollView content: it scrolls with the
            // text and paints above the input, WITHOUT touching the TextField's internal
            // hierarchy (foreign children inside TextInput interfere with the text engine).
            textField.parent.Add(mirrorElement);
            source.RegisterCallback<GeometryChangedEvent>(_ => SyncMentionMirror(textArea));
            textField.RegisterCallback<GeometryChangedEvent>(_ => SyncMentionMirror(textArea));
            // Unity's multiline TextField scrolls its text via an internal ScrollView whose
            // scrolling does not raise GeometryChangedEvent — track its scrollers too.
            if (innerScroll != null)
            {
                innerScroll.verticalScroller.valueChanged += _ => SyncMentionMirror(textArea);
                innerScroll.horizontalScroller.valueChanged += _ => SyncMentionMirror(textArea);
            }
            SyncMentionMirror(textArea);
            RefreshMentionMirror(textArea, textArea.value);
        }

        private static void SyncMentionMirror(TextArea textArea)
        {
            if (!s_MentionMirrors.TryGetValue(textArea, out var mirror)) return;
            var source = mirror.Source;
            var element = mirror.Element;
            var host = element?.parent;
            if (source == null || element == null || host == null || source.panel == null) return;

            if (mirror.TextColorHex == null && !float.IsNaN(source.layout.width) && source.layout.width > 0
                && source.resolvedStyle.color.a > 0f)
            {
                // First laid-out sync: capture the theme text color for the mirror's plain
                // runs, then hide the editable ink — from here on the mirror is the only
                // visible text layer. The zero-alpha guard keeps already-hidden ink (e.g.
                // after a re-attach) from being frozen as the mirror color.
                mirror.TextColorHex = "#" + ColorUtility.ToHtmlStringRGBA(source.resolvedStyle.color);
                // The mirror's own base becomes the theme color (opaque, so per-run
                // rich-text colors still combine) so plain text can render without markup.
                element.style.color = source.resolvedStyle.color;
                source.style.color = Color.clear;
                RefreshMentionMirror(textArea, textArea.value);
            }

            // Place the mirror over the source's CONTENT rect (the area its glyphs render
            // in) using pure LAYOUT coordinates: walk the layout positions from the source
            // up to the host. Layout values are immune to transforms, which is essential —
            // measuring via worldBound raced transform updates (the keyboard-float
            // translate, world-space XR panels) and permanently parked the mirror off the
            // input. Scrolling inside the field IS transform-based, so it is reapplied
            // explicitly from the inner ScrollView's scrollOffset (a plain property).
            Vector2 targetPos = source.contentRect.position;
            var walker = (VisualElement)source;
            while (walker != null && walker != host)
            {
                targetPos += walker.layout.position;
                walker = walker.hierarchy.parent;
            }
            if (walker == null) return; // Source is no longer under the host.
            if (mirror.InnerScroll != null)
            {
                targetPos -= mirror.InnerScroll.scrollOffset;
            }
            if (float.IsNaN(targetPos.x) || float.IsNaN(targetPos.y)
                || float.IsNaN(source.contentRect.width) || float.IsNaN(source.contentRect.height)) return;

            var style = element.style;
            style.width = source.contentRect.width;
            style.height = source.contentRect.height;
            // Absolute insets are relative to the host's padding box, while the walk
            // produced border-box coordinates — the host's border widths come off.
            style.left = targetPos.x - host.resolvedStyle.borderLeftWidth;
            style.top = targetPos.y - host.resolvedStyle.borderTopWidth;

            // Text shaping must match the editable element exactly so wrap points align.
            var resolved = source.resolvedStyle;
            style.whiteSpace = resolved.whiteSpace;
            style.fontSize = resolved.fontSize;
            style.unityFontStyleAndWeight = resolved.unityFontStyleAndWeight;
            style.letterSpacing = resolved.letterSpacing;
            style.wordSpacing = resolved.wordSpacing;
            style.unityParagraphSpacing = resolved.unityParagraphSpacing;
            style.unityTextAlign = resolved.unityTextAlign;
        }

        private static void RefreshMentionMirror(TextArea textArea, string plainText)
        {
            if (textArea == null) return;
            if (!s_MentionMirrors.TryGetValue(textArea, out var mirror) || mirror.Element == null) return;
            // CRITICAL: never use the .text setter here. TextElement implements
            // INotifyValueChanged<string>, so .text fires a ChangeEvent<string> that bubbles
            // into the TextArea's input handling and gets adopted as the composer VALUE
            // (turning the field into our markup, then into empty). SetValueWithoutNotify
            // updates the rendered text silently.
            string content;
            if (mirror.TextColorHex == null)
            {
                content = string.Empty;
            }
            else
            {
                var tracker = GetTracker(textArea);
                // Fast path for the common case: no mention runs and nothing the rich-text
                // parser could interpret — assign the plain text directly (the mirror's own
                // base color is the captured theme color) instead of rebuilding the full
                // markup on every keystroke.
                content = tracker.Spans.Count == 0 && (plainText == null || plainText.IndexOf('<') < 0)
                    ? plainText ?? string.Empty
                    : BuildMirrorText(plainText, tracker, mirror.TextColorHex);
            }
            ((INotifyValueChanged<string>)mirror.Element).SetValueWithoutNotify(content);
        }

        private static string BuildMirrorText(string plainText, MentionSpanTracker tracker, string baseColorHex)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            var spans = tracker.Spans;
            var builder = new StringBuilder(plainText.Length + spans.Count * 32 + 32);
            int cursor = 0;
            foreach (var span in spans)
            {
                if (span.Start < cursor || span.End > plainText.Length) continue;
                AppendSegment(builder, plainText.Substring(cursor, span.Start - cursor), baseColorHex);
                AppendSegment(builder, plainText.Substring(span.Start, span.Length), k_MentionColorHex);
                cursor = span.End;
            }
            AppendSegment(builder, plainText.Substring(cursor), baseColorHex);
            return builder.ToString();

            static void AppendSegment(StringBuilder builder, string segment, string colorHex)
            {
                if (string.IsNullOrEmpty(segment)) return;
                builder.Append("<color=").Append(colorHex).Append('>')
                    .Append(EscapeForRichText(segment))
                    .Append("</color>");
            }
        }

        // Rich text is on for the mirror, so user-typed markup must render literally: wrap
        // segments in <noparse>, splitting any literal "</noparse>" across a close/reopen
        // boundary so it can't terminate the block early.
        private static string EscapeForRichText(string segment)
        {
            return "<noparse>" + segment.Replace("</noparse>", "</</noparse><noparse>noparse>") + "</noparse>";
        }

        // How often a focused composer is polled for text changes the value-changing events
        // may have missed (on-screen keyboards can commit text in batches).
        private const long k_MentionPollIntervalMs = 150;

        private class MentionInputBinding
        {
            public IVisualElementScheduledItem Poller;
            public IVisualElementScheduledItem NavPoller;
            public IVisualElementScheduledItem TypeaheadRecovery;
            public IVisualElementScheduledItem PendingCommit;
            public Func<AssetInfo?> AssetInfoProvider;
            public UnityEngine.UIElements.TextField TextField; // Inner field, resolved once.
            public bool HasFocus;
            public float KeyboardOffset;
            // Index of the space auto-inserted by the last mention completion; -1 when
            // none is pending. Armed by InsertNameTagging, consumed by the next edit.
            public int PendingAutoSpace = -1;
        }

        // The composer that most recently ran the typeahead; used to recover it after a
        // keyboard device transition. Cleared when that composer's mentions are cleared.
        private static TextArea s_ActiveTypeaheadComposer;

        /// <summary>
        /// On Android, connecting or disconnecting an input device (e.g. a Bluetooth
        /// keyboard) restarts the platform input pipeline, which blurs the focused field
        /// and dismisses the suggestion popover mid-typeahead. This retries for a couple
        /// of seconds: restore focus first, then re-open the popover with the filter it
        /// had — everything needed (the '@' anchor and the typed filter) is still in the
        /// composer's userData, validated against the current text before reopening.
        /// </summary>
        private static void ScheduleTypeaheadRecovery(TextArea textArea)
        {
            if (!ReferenceEquals(s_ActiveTypeaheadComposer, textArea)) return;
            if (!s_InputBindings.TryGetValue(textArea, out var binding)) return;

            binding.TypeaheadRecovery?.Pause();
            int attemptsLeft = 30; // ~5s at 166ms — BT handshake + input restart settle asynchronously.
            IVisualElementScheduledItem recovery = null;
            recovery = textArea.schedule.Execute(() =>
            {
                attemptsLeft--;
                if (attemptsLeft <= 0 || TryRecoverTypeahead(textArea, binding))
                {
                    recovery.Pause();
                }
            }).Every(166);
            binding.TypeaheadRecovery = recovery;
        }

        // Returns true when recovery is finished (popover alive again) or no longer
        // applicable (the pending '@' filter is gone, completed, or was never valid).
        private static bool TryRecoverTypeahead(TextArea textArea, MentionInputBinding binding)
        {
            if (!ReferenceEquals(s_ActiveTypeaheadComposer, textArea)) return true;
            if (textArea.userData is not (int anchorIndex, string searchName)) return true;

            var textField = textArea.Q<UnityEngine.UIElements.TextField>();
            var value = textField?.value ?? string.Empty;
            searchName ??= string.Empty;
            if (anchorIndex <= 0 || anchorIndex > value.Length || value[anchorIndex - 1] != '@') return true;
            if (anchorIndex + searchName.Length > value.Length) return true;
            if (!string.Equals(value.Substring(anchorIndex, searchName.Length), searchName, StringComparison.Ordinal)) return true;

            var spans = GetTracker(textArea).Spans;
            for (int i = 0; i < spans.Count; i++)
            {
                if (spans[i].Start == anchorIndex - 1) return true; // Already completed into a mention.
            }

            // The teardown (blur + popover dismissal) lands hundreds of milliseconds
            // AFTER the device-change event, so "everything still healthy" means keep
            // watching until the window expires — not that there is nothing to fix.
            if (NamePopover != null)
            {
                if (binding.HasFocus) return false;
                // Popover survived but the composer lost focus: take focus back.
                textArea.Focus();
                return false;
            }

            if (!binding.HasFocus)
            {
                textArea.Focus();
                return false; // Let focus settle before reopening the popover.
            }

            var assetInfo = binding.AssetInfoProvider?.Invoke();
            if (assetInfo == null || !assetInfo.HasValue) return false;
            ShowNameSuggestion(assetInfo.Value.Asset.Descriptor.OrganizationId, anchorIndex, textArea, searchName);
            return NamePopover != null;
        }

        // Gap kept between the composer's bottom edge and the on-screen keyboard.
        private const float k_KeyboardClearanceMargin = 8f;

        // Floats the focused composer up just enough to clear the on-screen keyboard (and
        // back once it hides). With the OS input companion field hidden (hideMobileInput),
        // the composer is the only place the user can see what they type, so it must not
        // sit underneath the keyboard. Only the keyboard's HEIGHT is used — its rect origin
        // conventions vary by platform, but it always docks to the bottom edge. The mention
        // mirror is a child of the TextArea, so it follows the translation automatically.
        private static void AdjustComposerForKeyboard(TextArea textArea, MentionInputBinding binding)
        {
            float target = 0f;
            if (textArea.panel != null)
            {
                float keyboardHeight = SoftKeyboardMetrics.GetHeightPixels();
                if (keyboardHeight > 0f)
                {
                    float keyboardTopPanelY = RuntimePanelUtils.ScreenToPanel(textArea.panel,
                        new Vector2(0f, Screen.height - keyboardHeight)).y;
                    // worldBound already includes the current translation; add it back to
                    // measure from the composer's resting position.
                    float restingBottom = textArea.worldBound.yMax + binding.KeyboardOffset;
                    float overlap = restingBottom + k_KeyboardClearanceMargin - keyboardTopPanelY;
                    if (overlap > 0f)
                    {
                        target = overlap;
                    }
                }
            }

            if (!Mathf.Approximately(target, binding.KeyboardOffset))
            {
                binding.KeyboardOffset = target;
                textArea.style.translate = new Translate(0f, -target);
            }
        }

        private static readonly ConditionalWeakTable<TextArea, MentionInputBinding> s_InputBindings = new();

        /// <summary>
        /// Completes a composer's mention input pipeline so it works with every input source:
        /// a focused-only poll feeds the same change processing as the value-changing events
        /// (deduped via the tracker's LastObservedText snapshot), catching on-screen-keyboard
        /// commits that never fire per-keystroke events. It also watches input device changes
        /// — WITHOUT blurring the field — so a hardware keyboard (e.g. an iPad keyboard
        /// cover) can attach or detach mid-edit while text, spans, highlight, and the
        /// typeahead caret survive untouched.
        /// </summary>
        /// <summary>
        /// One-stop mention support for a composer: hides the OS companion input bar,
        /// forces the buffer to plain text, attaches the highlight mirror, and enables
        /// the input pipeline. BOTH mention composers (the reply box and the annotation
        /// edit box) must initialize through here so their behavior can never diverge.
        /// </summary>
        public static void SetupMentionComposer(TextArea textArea, Func<AssetInfo?> assetInfoProvider)
        {
            var textField = textArea?.Q<UnityEngine.UIElements.TextField>();
            if (textField == null) return;
            // Always hide the OS input companion field: the composer itself is the text
            // view, and the native bar (with its own accept/cancel buttons) duplicates it.
            // The old "Keyboard.current == null" heuristic is unreliable — iOS always
            // reports a keyboard device even without one attached.
            textField.hideMobileInput = true;
            // The composer buffer is always plain text (mentions live in MentionSpanTracker,
            // not as markup in the field), so rich text stays off regardless of input device.
            textField.Q<TextElement>().enableRichText = false;
            // The highlight overlay that renders confirmed mentions in color.
            AttachMentionMirror(textArea);
            // Poll + device-change handling so mentions work with on-screen keyboards and
            // survive a hardware keyboard attaching/detaching mid-edit.
            EnableMentionInputPipeline(textArea, assetInfoProvider);
        }

        public static void EnableMentionInputPipeline(TextArea textArea, Func<AssetInfo?> assetInfoProvider)
        {
            if (textArea == null || assetInfoProvider == null) return;
            if (s_InputBindings.TryGetValue(textArea, out _)) return; // Once per composer.

            var textField = textArea.Q<UnityEngine.UIElements.TextField>();
            if (textField == null) return;

            // A composer is a chat box, not a form field: focusing it must never select
            // the whole message — with everything selected, the first keystroke of a new
            // touch-keyboard session REPLACES the entire text (seen on Quest when
            // refocusing after an Enter-committed mention). The caret instead stays
            // where it was left (e.g. right after an inserted mention).
            textField.textSelection.selectAllOnFocus = false;
            textField.textSelection.selectAllOnMouseUp = false;

            var binding = new MentionInputBinding { AssetInfoProvider = assetInfoProvider, TextField = textField };
            s_InputBindings.Add(textArea, binding);

            // iOS: hardware keys are invisible to Unity, so a tiny native plugin observes
            // them for the popover navigation. Inert stub everywhere else, idempotent here.
            MentionHardwareKeys.Initialize();

            // Poll the INNER field: it always reflects the real buffer, whereas the TextArea's
            // cached value only syncs on change events (which is exactly what can be missing).
            binding.Poller = textArea.schedule.Execute(() =>
            {
                AdjustComposerForKeyboard(textArea, binding);
                // Text/span/mirror upkeep must run even while the selected asset is
                // transiently unavailable (the editable ink is transparent — a stale
                // mirror would be the only visible text); only the typeahead itself
                // needs the asset, gated inside ProcessTextChange.
                ProcessTextChange(assetInfoProvider(), textArea, textField.value);
            }).Every(k_MentionPollIntervalMs);
            binding.Poller.Pause();

            textArea.RegisterCallback<FocusInEvent>(_ =>
            {
                binding.HasFocus = true;
                binding.Poller?.Resume();
            });
            textArea.RegisterCallback<FocusOutEvent>(_ =>
            {
                binding.HasFocus = false;
                binding.Poller?.Pause();
                if (binding.KeyboardOffset != 0f)
                {
                    binding.KeyboardOffset = 0f;
                    textArea.style.translate = new Translate(0f, 0f);
                }
            });

            // Opening the suggestion popover grabs focus (App UI focuses its root element),
            // which blurs the composer for an instant before PopoverMenuOnShown hands focus
            // straight back. On Android that instant closes and re-opens the soft keyboard —
            // a visible blink. When the blur's only cause is our own popover, swallow the
            // event before the text input reacts, so the keyboard session never closes.
            // Mobile-only: desktop has no visible symptom and keeps stock focus behavior.
            if (Application.isMobilePlatform)
            {
                textArea.RegisterCallback<FocusOutEvent>(evt =>
                {
                    if (NamePopover != null && ReferenceEquals(evt.relatedTarget, NamePopover.GetMovableElement()))
                    {
                        evt.StopImmediatePropagation();
                    }
                }, TrickleDown.TrickleDown);
            }

            // Subscribe only while attached: an eager subscription on this process-lifetime
            // static event would pin the composer's whole subtree (one closure per annotation
            // entry), and a composer that never gets attached would leak its handler forever.
            if (textArea.panel != null)
            {
                InputSystem.onDeviceChange += OnDeviceChange;
            }
            textArea.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                InputSystem.onDeviceChange -= OnDeviceChange;
                InputSystem.onDeviceChange += OnDeviceChange;
            });
            textArea.RegisterCallback<DetachFromPanelEvent>(_ => InputSystem.onDeviceChange -= OnDeviceChange);
            return;

            void OnDeviceChange(InputDevice device, InputDeviceChange change)
            {
                // Bluetooth devices are kept alive across sessions by the Input System:
                // only the FIRST attach is Added — later cycles report Disconnected /
                // Reconnected, so those must trigger the recovery too.
                if (change != InputDeviceChange.Added && change != InputDeviceChange.Removed
                    && change != InputDeviceChange.Disconnected && change != InputDeviceChange.Reconnected) return;
                // Only when this composer's typeahead is OPEN at event time: the teardown
                // being recovered from always arrives after the event, whereas an
                // abandoned '@' from minutes ago must not steal focus and resurrect the
                // popover whenever some unrelated device connects or disconnects.
                if (NamePopover == null || !ReferenceEquals(s_ActiveTypeaheadComposer, textArea)) return;
                // A hardware keyboard attaching/detaching mid-typeahead recreates the platform
                // keyboard session, which can re-seat its caret arbitrarily (seen on iPad: the
                // caret lands right after the '@'). Re-assert the filter-end caret while the
                // new session settles; the popover nav poll performs the restores.
                s_CaretRestoreUntilTime = Time.realtimeSinceStartupAsDouble + k_DeviceChangeCaretRestoreSeconds;
                // On Android the same transition also blurs the field and dismisses the
                // popover; bring both back. NOT on XR headsets: opening/closing the system
                // keyboard overlay is itself a device change there, so recovery would
                // refocus the composer and summon the keyboard right back whenever the
                // user tries to close it.
                if (!UsesXRSystemKeyboard)
                {
                    ScheduleTypeaheadRecovery(textArea);
                }
            }
        }

        // Moves the highlighted row in the open suggestion popover (direction -1 = up, +1 =
        // down, wrapping). Shared by the desktop KeyDown path and the mobile Input System poll.
        private static void MoveNameSuggestionSelection(int direction)
        {
            if (NamePopover == null) return;
            var buttons = NamePopover.containerView.Query<ActionButton>().ToList();
            if (buttons.Count == 0) return;
            int currentIndex = -1;
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i].selected)
                {
                    currentIndex = i;
                    break;
                }
            }

            currentIndex = direction < 0
                ? (currentIndex <= 0 ? buttons.Count - 1 : currentIndex - 1)
                : (currentIndex >= buttons.Count - 1 ? 0 : currentIndex + 1);

            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].selected = i == currentIndex;
            }
        }

        private static bool TryGetSelectedSuggestion(out UserTaggingButtonController controller)
        {
            controller = null;
            if (NamePopover == null) return false;
            var selectedButton = NamePopover.containerView.Query<ActionButton>()
                .Where(b => b.selected).First();
            controller = selectedButton?.userData as UserTaggingButtonController;
            return controller != null;
        }

        // Drives list navigation from the Input System while the popover is open on touch
        // platforms, where hardware-keyboard key events (e.g. an iPad keyboard cover) are
        // consumed by the native text input and never reach UI Toolkit. Runs every panel
        // update so wasPressedThisFrame is never missed.
        // How many poll ticks to keep re-asserting the caret after an arrow press. The
        // native key handling can move the platform caret after our first restore runs,
        // so a second tick wins that ordering race.
        private static int s_CaretRestoreTicks;

        // Restore window after a keyboard device change: recreating the platform keyboard
        // session (e.g. detaching an iPad keyboard cover mid-typeahead) takes several
        // hundred milliseconds — occasionally over a second — before the new session
        // seats its caret. WALL-CLOCK, not poll ticks: the nav poll runs once per panel
        // update, so a tick count halves on a 120Hz iPad (a 60-tick "second" became
        // ~0.5s there, and slow session settles escaped the window — the caret then
        // stuck wherever the new session put it, e.g. the start of the message).
        private const float k_DeviceChangeCaretRestoreSeconds = 2f;
        private static double s_CaretRestoreUntilTime;

        // Hardware-keyboard presence as seen by the native plugin at the previous nav-poll
        // tick. iOS never reports cover attach/detach through the Input System (its keyboard
        // device is permanent), so transitions are detected from GCKeyboard instead.
        private static bool s_HadHardwareKeyboardAtLastPoll;

        // Session status at the previous nav-poll tick; seeded when the popover opens.
        private static TouchScreenKeyboard.Status s_LastNavKeyboardStatus = TouchScreenKeyboard.Status.Canceled;

        // Quest-verified system-keyboard semantics: Enter ends the session with Status.Done
        // (no key events, no newline), the keyboard overlay is modal (captures the
        // controller rays), and opening/closing it storms onDeviceChange. Other XR
        // runtimes are ASSUMED to behave the same until verified on hardware — when a
        // second headset target lands (e.g. Android XR glasses), audit every use of this
        // predicate and split per-runtime if its keyboard reports Done for a plain
        // dismissal.
        private static bool UsesXRSystemKeyboard => UnityEngine.XR.XRSettings.isDeviceActive;

        // Quest's system keyboard commits with a session-status change instead of key
        // events or a newline: pressing Enter reports Status.Done and closes the keyboard
        // (nothing reaches UI Toolkit). Treat the Visible -> Done transition while the
        // popover is open as confirming the highlighted suggestion. XR-only: on phones
        // and tablets Enter already arrives as a newline in the text, and Done there
        // means the user merely dismissed the keyboard.
        private static bool PollKeyboardCommit(TextArea textArea, MentionInputBinding binding)
        {
            if (!UsesXRSystemKeyboard) return false;
            var textField = binding.TextField;
            var session = textField?.touchScreenKeyboard;
            if (session == null) return false;

            var status = session.status;
            var previous = s_LastNavKeyboardStatus;
            s_LastNavKeyboardStatus = status;
            if (status != TouchScreenKeyboard.Status.Done || previous != TouchScreenKeyboard.Status.Visible) return false;
            if (!TryGetSelectedSuggestion(out var controller)) return false;

            NamePopover?.Dismiss();
            ScheduleDeferredCommit(textArea, binding, textField, controller.UserInfo);
            return true;
        }

        // How long the deferred commit may wait for the dead session's teardown before
        // inserting anyway (the blur normally lands within ~200ms of Done).
        private const int k_DeferredCommitMaxTicks = 12;

        // The insert must WAIT OUT the session teardown: between Done and the blur, UI
        // Toolkit still syncs the field FROM the dead session's text, which would
        // overwrite an immediately-inserted mention with the pre-insert text (seen on
        // Quest as a flash of blue reverting to "@Der"). Rather than a fixed delay, the
        // insert is keyed to the observable end of the teardown — session inactive AND
        // composer blurred — with a capped fallback, and abandons itself if the composer
        // is edited, reset (ClearMentions pauses it), or detached while waiting.
        // The UserInfo is captured up front: the dismissal detaches the popover's buttons,
        // whose Dispose() drops the button controller's state.
        private static void ScheduleDeferredCommit(TextArea textArea, MentionInputBinding binding, UnityEngine.UIElements.TextField textField, IUserInfo userInfo)
        {
            binding.PendingCommit?.Pause();
            var pendingText = textField.value;
            int attemptsLeft = k_DeferredCommitMaxTicks;
            IVisualElementScheduledItem commit = null;
            commit = textArea.schedule.Execute(() =>
            {
                attemptsLeft--;
                if (textArea.panel == null || !string.Equals(textField.value, pendingText, StringComparison.Ordinal))
                {
                    commit.Pause();
                    return;
                }
                var session = textField.touchScreenKeyboard;
                bool teardownDone = !binding.HasFocus && (session == null || !session.active);
                if (!teardownDone && attemptsLeft > 0) return;
                commit.Pause();

                InsertNameTagging(userInfo, textArea);
                // Keep the composing flow going: refocus summons a fresh keyboard
                // session so typing can continue right after the inserted mention
                // (select-all-on-focus is disabled, so nothing gets overwritten). The
                // new session takes a few hundred ms to come up and reseats its caret,
                // so the caret assertion is re-armed with a window long enough to
                // cover it. Closing the keyboard still works — the XR recovery gate
                // keeps it from being resurrected.
                textArea.Focus();
                SchedulePostInsertCaret(textArea, textField, textField.value, textField.cursorIndex, k_RefocusCaretTicks);
            }).Every(50);
            binding.PendingCommit = commit;
        }

        private static void PollHardwareNavigation(TextArea textArea)
        {
            if (NamePopover == null) return;
            if (!s_InputBindings.TryGetValue(textArea, out var binding)) return;
            if (PollKeyboardCommit(textArea, binding)) return;

            bool up = false;
            bool down = false;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                up = keyboard.upArrowKey.wasPressedThisFrame;
                down = keyboard.downArrowKey.wasPressedThisFrame;
            }
            // iOS surfaces hardware keys only through the native plugin; consume both
            // flags every tick so neither goes stale.
            up |= MentionHardwareKeys.ConsumeUpPressed();
            down |= MentionHardwareKeys.ConsumeDownPressed();

            // A cover attach/detach while the typeahead is open recreates the platform
            // keyboard session, which re-seats its caret arbitrarily (seen on iPad: the
            // caret lands right after the '@'). Only GCKeyboard sees the transition on
            // iOS, so detect it here and re-assert the filter-end caret while the new
            // session settles.
            bool hasHardwareKeyboard = MentionHardwareKeys.HasHardwareKeyboard;
            if (hasHardwareKeyboard != s_HadHardwareKeyboardAtLastPoll)
            {
                s_HadHardwareKeyboardAtLastPoll = hasHardwareKeyboard;
                s_CaretRestoreUntilTime = Time.realtimeSinceStartupAsDouble + k_DeviceChangeCaretRestoreSeconds;
            }

            if (up)
            {
                MoveNameSuggestionSelection(-1);
                s_CaretRestoreTicks = 2;
            }
            else if (down)
            {
                MoveNameSuggestionSelection(+1);
                s_CaretRestoreTicks = 2;
            }

            if (s_CaretRestoreTicks > 0 || Time.realtimeSinceStartupAsDouble < s_CaretRestoreUntilTime)
            {
                if (s_CaretRestoreTicks > 0)
                {
                    s_CaretRestoreTicks--;
                }
                RestoreTypeaheadCaret(textArea, binding.TextField);
            }
        }

        // The key observation is non-consuming, so the platform text session ALSO handles
        // the arrow and moves its caret (on iOS, up = jump to the start of the text —
        // continued typing would land there). Put the caret back at the end of the
        // typeahead filter.
        private static void RestoreTypeaheadCaret(TextArea textArea, UnityEngine.UIElements.TextField textField)
        {
            if (textArea?.userData is not (int anchorIndex, string searchName)) return;
            if (textField == null) return;

            int caret = Mathf.Min(anchorIndex + (searchName?.Length ?? 0), textField.value?.Length ?? 0);
            SetComposerCaret(textField, caret);
        }

        // Sets the composer caret in both layers: UI Toolkit's cursor AND the platform
        // keyboard session's selection — on mobile the session is the source of truth
        // (the field re-syncs from it every frame), so its selection must be set too,
        // not just the UI Toolkit cursor. Only while the session is alive: driving the
        // selection of a torn-down session (e.g. right after Quest's keyboard closed on
        // Enter) can crash the platform keyboard layer.
        private static void SetComposerCaret(UnityEngine.UIElements.TextField textField, int caret)
        {
            textField.cursorIndex = caret;
            textField.selectIndex = caret;
            var nativeKeyboard = textField.touchScreenKeyboard;
            if (nativeKeyboard != null && nativeKeyboard.active)
            {
                nativeKeyboard.selection = new RangeInt(caret, 0);
            }
        }

        private static void MakeNameSuggestionButton(TextArea textArea, Popover popover, VisualElement parent, List<IUserInfo> suggestedNames)
        {
            bool first = true;
            foreach (var userDataKeyName in suggestedNames)
            {
                var nameButton = new NameSuggestionButton
                {
                    selected = first
                };
                first = false;
                var newUserButton = new UserTaggingButtonController(
                    userDataKeyName, 
                    nameButton, textArea, ref popover);
                nameButton.userData = newUserButton;
                
                parent.Add(nameButton);
            }
        }

        public static void InsertNameTagging(IUserInfo userInfo, TextArea textArea)
        {
            // Callable from deferred/scheduled paths: throwing inside a UI Toolkit
            // callback breaks the panel's dispatch loop (freezes ALL UI), so bail
            // instead of dereferencing anything that may have been torn down.
            if (userInfo == null || textArea == null) return;
            var anchorIndex = 0;
            string searchValue = string.Empty;
            if (textArea.userData is (int i, string s))
            {
                anchorIndex = i;
                searchValue = s;
            }
            var textField = textArea.Q<UnityEngine.UIElements.TextField>();
            var currentValue = textField.value;

            // The anchor points right after the '@' that opened the popover; bail if it's
            // gone, and only replace text that actually IS "@" + the recorded filter —
            // userData can lag the buffer (async member fetch racing typing, edits landing
            // inside the poll window), and removing anything else corrupts the message.
            searchValue ??= string.Empty;
            if (anchorIndex <= 0 || anchorIndex > currentValue.Length || currentValue[anchorIndex - 1] != '@') return;
            if (anchorIndex + searchValue.Length > currentValue.Length
                || !string.Equals(currentValue.Substring(anchorIndex, searchValue.Length), searchValue, StringComparison.Ordinal)) return;

            // Replace the '@' + typed partial name with the visible mention text (plain, no
            // markup) followed by a space so subsequent typing lands outside the span.
            int atIndex = anchorIndex - 1;
            int removeLength = 1 + searchValue.Length;
            var safeName = SanitizeMentionName(userInfo.Name);
            var visibleTag = "@" + safeName;
            var newText = currentValue.Remove(atIndex, removeLength).Insert(atIndex, visibleTag + " ");

            // Run the replacement through the tracker (shifts existing spans), register the
            // new span, then commit buffer + snapshot + mirror together.
            var tracker = GetTracker(textArea);
            tracker.ApplyTextChange(currentValue, newText);
            tracker.AddSpan(atIndex, visibleTag.Length, userInfo.UserId.ToString(), safeName);
            CommitProgrammaticText(textArea, tracker, newText);

            var newCursorPos = atIndex + visibleTag.Length + 1;
            textField.selectIndex = newCursorPos;
            textField.cursorIndex = newCursorPos;
            SchedulePostInsertCaret(textArea, textField, newText, newCursorPos);

            if (s_InputBindings.TryGetValue(textArea, out var binding))
            {
                binding.PendingAutoSpace = atIndex + visibleTag.Length;
            }
        }

        // How many frames to keep re-asserting the caret after a mention is inserted.
        // Unity pushes the replaced text into the platform keyboard session after the
        // insert returns, and that push resets the session's selection to the end of
        // the text — the per-frame native→UI Toolkit sync would then drag the caret
        // there too (visible when the mention sits mid-text, with words after it).
        private const int k_PostInsertCaretTicks = 5;

        // Longer window used when a fresh keyboard session is being summoned around the
        // caret assertion (session startup takes a few hundred ms and reseats the caret).
        private const int k_RefocusCaretTicks = 40;

        private static void SchedulePostInsertCaret(TextArea textArea, UnityEngine.UIElements.TextField textField, string insertedText, int caret, int ticks = k_PostInsertCaretTicks)
        {
            int remainingTicks = ticks;
            textArea.schedule.Execute(() =>
            {
                remainingTicks--;
                // The user resumed editing; stop fighting them for the caret.
                if (!string.Equals(textField.value, insertedText, StringComparison.Ordinal))
                {
                    remainingTicks = 0;
                    return;
                }
                SetComposerCaret(textField, caret);
            }).Every(16).Until(() => remainingTicks <= 0);
        }

        public static string ParseUserTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return Regex.Replace(text, k_CloudMentionPattern, $"<color={k_MentionColorHex}>@$1</color>");
        }
        
        public static bool RepeatedAttachment(GridView gridView, IAnnotation annotation, string filePath, out AddAttachmentFailType type)
        {
            var existingList = gridView.itemsSource as List<Attachment>;
            var fileName = Path.GetFileName(filePath);
            if (existingList != null)
            {
                if (existingList.Any(x =>
                        string.Equals(x.FilePath, filePath, StringComparison.CurrentCultureIgnoreCase)))
                {
                    type = AddAttachmentFailType.DuplicateFilePath;
                    return true;
                }
                if (existingList.Any(x =>
                        string.Equals(x.FileName, fileName, StringComparison.CurrentCultureIgnoreCase)))
                {
                    type = AddAttachmentFailType.DuplicateFileName;
                    return true;
                }
            }

            if (annotation == null || annotation.Attachments == null || annotation.Attachments.Count <= 0)
            {
                type = AddAttachmentFailType.None;
                return false;
            }
            
            foreach (var annotationAttachment in annotation.Attachments)
            {
                if (annotationAttachment is not IFileAttachment fileAttachment) continue;
                if (string.Equals(fileAttachment.FilePath, fileName))
                {
                    type = AddAttachmentFailType.DuplicateFileName;
                    return true;
                }
            }
            type = AddAttachmentFailType.None;
            return false;
        }

        // Drives the @-mention typeahead over a plain-text composer: keeps the span model in
        // sync with every edit and opens/filters the name-suggestion popover.
        public static void OnTextAreaValueChanging(AssetInfo assetInfo, ChangingEvent<string> evt)
        {
            var textArea = evt.target as TextArea;
            if(textArea == null) return;
            ProcessTextChange(assetInfo, textArea, evt.newValue);
        }

        // Single entry point for composer text changes, fed by both the value-changing events
        // and the focused poll (EnableMentionInputPipeline). The previous text always comes
        // from the tracker's LastObservedText snapshot, so the two sources dedupe naturally.
        // Punctuation that swallows the auto-inserted space when typed directly after a
        // completed mention (Slack behavior): "@Name ," becomes "@Name,". The apostrophe
        // makes possessives read naturally ("@Name's"); the full-width set covers CJK.
        private const string k_AutoSpaceSwallowingPunctuation = ",.!?;:'\")]}、，。！？；：";

        // Slack-style smart punctuation: single-shot, armed by InsertNameTagging and
        // consumed by whatever edit comes next. Only a lone punctuation character typed
        // right after the auto-space rewrites the text; anything else is left alone.
        private static bool TrySwallowAutoSpace(TextArea textArea, MentionSpanTracker tracker, string previousText, string newText)
        {
            if (!s_InputBindings.TryGetValue(textArea, out var binding)) return false;
            int spaceIndex = binding.PendingAutoSpace;
            if (spaceIndex < 0) return false;
            binding.PendingAutoSpace = -1;

            if (newText.Length != previousText.Length + 1) return false;
            int insertPos = tracker.LastEditEnd - 1;
            if (insertPos != spaceIndex + 1) return false;
            if (spaceIndex >= previousText.Length || previousText[spaceIndex] != ' ') return false;
            if (k_AutoSpaceSwallowingPunctuation.IndexOf(newText[insertPos]) < 0) return false;
            // Never fuse the punctuation onto a following word: mid-text, the auto-space
            // may be the only separator before pre-existing text.
            if (insertPos + 1 < newText.Length && !char.IsWhiteSpace(newText[insertPos + 1])) return false;

            // Drop the auto-space so the punctuation sits tight after the mention. The
            // mention span ends exactly where the space was, so it survives the removal.
            var corrected = newText.Remove(spaceIndex, 1);
            tracker.ApplyTextChange(newText, corrected);
            CommitProgrammaticText(textArea, tracker, corrected);

            var textField = textArea.Q<UnityEngine.UIElements.TextField>();
            if (textField != null)
            {
                int caret = spaceIndex + 1; // Right after the punctuation.
                textField.cursorIndex = caret;
                textField.selectIndex = caret;
                SchedulePostInsertCaret(textArea, textField, corrected, caret);
            }
            return true;
        }

        private static void ProcessTextChange(AssetInfo? assetInfo, TextArea textArea, string newValue)
        {
            var tracker = GetTracker(textArea);
            string previousText = tracker.LastObservedText ?? string.Empty;
            string newText = newValue ?? string.Empty;
            if (string.Equals(previousText, newText, StringComparison.Ordinal)) return;
            tracker.LastObservedText = newText;

            // Shift spans past the edit; dissolve any span the edit touched.
            tracker.ApplyTextChange(previousText, newText);

            if (TrySwallowAutoSpace(textArea, tracker, previousText, newText)) return;

            RefreshMentionMirror(textArea, newText);

            // Everything below is the typeahead, which needs the asset's organization;
            // the text/span/mirror upkeep above must run even while the selected asset
            // is transiently unavailable.
            if (!assetInfo.HasValue) return;
            OrganizationId organizationId = assetInfo.Value.Asset.Descriptor.OrganizationId;

            if (string.IsNullOrEmpty(newText))
            {
                if (ReferenceEquals(s_ActiveTypeaheadComposer, textArea))
                {
                    NamePopover?.Dismiss();
                }
                return;
            }

            // The popover-open branch only applies to the composer that OWNS the popover:
            // another composer's edits must not filter, dismiss, or rebind it with their
            // own (possibly stale) '@' anchor.
            if (NamePopover != null && ReferenceEquals(s_ActiveTypeaheadComposer, textArea))
            {
                // Popover already open: refresh the filter from the text between the '@' anchor
                // and the position of the edit that just happened.
                int anchorIndex = 0;
                if (textArea.userData is (int idx, string _))
                    anchorIndex = idx;

                int editEnd = tracker.LastEditEnd;
                if (anchorIndex <= 0 || anchorIndex > newText.Length
                    || newText[anchorIndex - 1] != '@' || editEnd < anchorIndex)
                {
                    // The triggering '@' is gone or the edit moved before it.
                    NamePopover.Dismiss();
                    return;
                }

                string searchName = newText.Substring(anchorIndex, editEnd - anchorIndex);
                int breakIndex = searchName.IndexOfAny(new[] { '\n', '\r' });
                if (breakIndex >= 0)
                {
                    // A single newline typed at the end of the filter while the list is open
                    // is Enter on a platform where hardware-key events never reach UI Toolkit
                    // (e.g. an iPad keyboard cover — iOS's native text input consumes them):
                    // treat it as confirming the highlighted suggestion. On desktop the
                    // KeyDown handler intercepts Enter before it can insert a newline, so
                    // this path and that one are mutually exclusive.
                    bool isSingleCharInsert = newText.Length == previousText.Length + 1;
                    bool breakAtEnd = breakIndex == searchName.Length - 1;
                    bool hasSelection = TryGetSelectedSuggestion(out var suggestion);
                    if (isSingleCharInsert && breakAtEnd && hasSelection)
                    {
                        var cleanedText = newText.Remove(anchorIndex + breakIndex, 1);
                        tracker.ApplyTextChange(newText, cleanedText);
                        CommitProgrammaticText(textArea, tracker, cleanedText);
                        textArea.userData = (anchorIndex, searchName.Substring(0, breakIndex));
                        NamePopover.Dismiss();
                        suggestion.Click();
                        return;
                    }
                    NamePopover.Dismiss();
                    return;
                }
                textArea.userData = (anchorIndex, searchName);

                var suggestedNames = ReturnSuggestedNames(organizationId, searchName);
                if (suggestedNames == null || suggestedNames.Count == 0)
                {
                    NamePopover.Dismiss();
                    return;
                }

                VisualElement parent = null;
                foreach (var nameButton in NamePopover.containerView.Query<NameSuggestionButton>().ToList())
                {
                    parent ??= nameButton.parent;
                    nameButton.RemoveFromHierarchy();
                }
                MakeNameSuggestionButton(textArea, NamePopover, parent, suggestedNames);
                return;
            }

            // A completed tag was just dissolved by this edit (e.g. backspacing into it):
            // Slack behavior — the tag un-pills and the suggestion list re-opens, querying
            // whatever is left between its '@' and the edit position. Only while the user
            // is actually editing: dissolves can also arrive from background text syncs
            // (e.g. a platform keyboard session tearing down), and those must not pop UI
            // on an unfocused composer.
            var dissolved = tracker.LastDissolvedSpan;
            if (dissolved != null && dissolved.Start < newText.Length && newText[dissolved.Start] == '@'
                && (!s_InputBindings.TryGetValue(textArea, out var dissolveBinding) || dissolveBinding.HasFocus))
            {
                int anchor = dissolved.Start + 1;
                int dissolvedEditEnd = tracker.LastEditEnd;
                if (dissolvedEditEnd >= anchor)
                {
                    string search = newText.Substring(anchor, dissolvedEditEnd - anchor);
                    if (search.IndexOf('\n') < 0)
                    {
                        if (m_OrganizationMembers == null || !m_OrganizationMembers.ContainsKey(organizationId))
                        {
                            _ = GetMemberAndSuggestNames(organizationId, anchor, textArea, search);
                        }
                        else
                        {
                            ShowNameSuggestion(organizationId, anchor, textArea, search);
                        }
                    }
                }
                return;
            }

            // Check if a single "@" was just inserted. The tracker's diff already located
            // the edit (LastEditEnd = position right after it) — reusing it keeps the
            // popover anchor and the span shifts agreeing on ambiguous inserts.
            if (newText.Length == previousText.Length + 1)
            {
                int diffIndex = tracker.LastEditEnd - 1;
                if (diffIndex >= 0 && diffIndex < newText.Length && newText[diffIndex] == '@')
                {
                    int startIndex = diffIndex + 1;
                    if (m_OrganizationMembers == null || !m_OrganizationMembers.ContainsKey(organizationId))
                    {
                        _ = GetMemberAndSuggestNames(organizationId, startIndex, textArea, string.Empty);
                    }
                    else
                    {
                        ShowNameSuggestion(organizationId, startIndex, textArea, string.Empty);
                    }
                }
            }
            return;

            async Task GetMemberAndSuggestNames(OrganizationId organizationId, int startIndex, TextArea ta, string initialSearch)
            {
                await GetOrgMembers(organizationId);
                ShowNameSuggestion(organizationId, startIndex, ta, initialSearch);
            }

        }

        private static void ShowNameSuggestion(OrganizationId organizationId, int startIndex, TextArea textArea, string initialSearch)
        {
            if (NamePopover != null)
            {
                // Single-popover model: an open list belonging to THIS composer means
                // nothing to do; one left open on ANOTHER composer is taken over (its
                // late dismissed callback tears down only its own composer's handlers).
                if (ReferenceEquals(s_ActiveTypeaheadComposer, textArea)) return;
                NamePopover.Dismiss();
                NamePopover = null;
            }

            var suggestedNames = ReturnSuggestedNames(organizationId, initialSearch);

            if(suggestedNames == null || suggestedNames.Count == 0) return;

            VisualElement nameSuggestionMenu = new VisualElement();

            nameSuggestionMenu.AddToClassList("thread-Popover-menu");
            textArea.userData = (startIndex, initialSearch);
            s_ActiveTypeaheadComposer = textArea;
            // Beside the composer on flat screens; ABOVE it (Slack-style) on headsets —
            // the world-space VR panels (notably the streaming scene's) have no side
            // room, which pushed a Start-placed popover away from the field entirely.
            // A bottom-docked composer always has the comment list's headroom above it.
            var placement = UsesXRSystemKeyboard
                ? PopoverPlacement.TopStart
                : PopoverPlacement.Start;
            NamePopover = Popover.Build(textArea, nameSuggestionMenu).SetArrowVisible(false)
                .SetPlacement(placement);

            MakeNameSuggestionButton(textArea, NamePopover, nameSuggestionMenu, suggestedNames);

            NamePopover.Show();

            NamePopover.shown += PopoverMenuOnShown;
            NamePopover.dismissed += NamePopoverOnDismissed;
            return;

            void PopoverMenuOnShown(Popover obj)
            {
                obj.shown -= PopoverMenuOnShown;
                textArea.RegisterCallback<KeyDownEvent>(OnKeyDownWithPopover, TrickleDown.TrickleDown);
                // On touch platforms hardware-keyboard events never reach UI Toolkit
                // (consumed by the native text input), so navigation is polled instead.
                if (Application.isMobilePlatform && s_InputBindings.TryGetValue(textArea, out var binding))
                {
                    // Seed the transition detectors so opening the popover doesn't count
                    // as a keyboard change (or a commit), and drain arrow presses latched
                    // while no popover was open so they can't pre-move the selection.
                    s_HadHardwareKeyboardAtLastPoll = MentionHardwareKeys.HasHardwareKeyboard;
                    MentionHardwareKeys.ConsumeUpPressed();
                    MentionHardwareKeys.ConsumeDownPressed();
                    s_LastNavKeyboardStatus = textArea.Q<UnityEngine.UIElements.TextField>()?.touchScreenKeyboard?.status
                        ?? TouchScreenKeyboard.Status.Canceled;
                    binding.NavPoller ??= textArea.schedule.Execute(() => PollHardwareNavigation(textArea)).Every(1);
                    binding.NavPoller.Resume();
                }
                textArea.Focus();
            }

            void NamePopoverOnDismissed(Popover popover, DismissType arg2)
            {
                popover.dismissed -= NamePopoverOnDismissed;
                // A dismissal callback can arrive late (dismissals are animated/async),
                // after a NEWER popover has already opened. The shared per-composer
                // handlers (the KeyDown registration and the nav poll) may only be torn
                // down when no live popover still relies on them: either this callback
                // belongs to the current popover, or the current popover lives on a
                // DIFFERENT composer (whose handlers are its own).
                bool isCurrent = ReferenceEquals(NamePopover, popover);
                if (isCurrent || !ReferenceEquals(s_ActiveTypeaheadComposer, textArea))
                {
                    textArea.UnregisterCallback<KeyDownEvent>(OnKeyDownWithPopover, TrickleDown.TrickleDown);
                    if (s_InputBindings.TryGetValue(textArea, out var binding))
                    {
                        binding.NavPoller?.Pause();
                    }
                }
                if (!isCurrent) return;
                JustDismissedPopover = true;
                Object.FindAnyObjectByType<CollaborationUIBase>()?.ResetDismissedPopover();
                NamePopover = null;
            }
        }

        private static void OnKeyDownWithPopover(KeyDownEvent keydownEvt)
        {
            if (NamePopover == null) return;
            if (keydownEvt.keyCode is KeyCode.UpArrow or KeyCode.DownArrow)
            {
                keydownEvt.StopPropagation();
                MoveNameSuggestionSelection(keydownEvt.keyCode == KeyCode.UpArrow ? -1 : +1);
                return;
            }

            if (keydownEvt.keyCode == KeyCode.Return || keydownEvt.keyCode == KeyCode.KeypadEnter)
            {
                keydownEvt.StopPropagation();
                shouldIgnoreKey = true;
                return;
            }

            if (shouldIgnoreKey && keydownEvt.keyCode == KeyCode.None)
            {
                keydownEvt.StopPropagation();
                if (!TryGetSelectedSuggestion(out var controller)) return;
                NamePopover?.Dismiss();
                controller.Click();
            }
            shouldIgnoreKey = false;
        }

        public static async Task<string> GetMemberName(OrganizationId orgId, string userId)
        {
            await GetOrgMembers(orgId);

            if (m_OrganizationMembers == null) return string.Empty;
            if (m_OrganizationMembers.TryGetValue(orgId, out var members))
            {
                var member = members.FirstOrDefault(x => string.Equals(x.UserId.ToString(), userId));
                if (member != null)
                {
                    return member.Name;
                }
            }
            return string.Empty;
        }
        
        // Single-flight, cached-per-org member fetch: concurrent callers await the SAME
        // in-flight task (no polling loop, no cross-org blocking — a fetch for one org
        // never delays lookups for another), an org that is already loaded returns
        // immediately, and a finished fetch removes itself so a failed one is simply
        // retried by the next caller instead of wedging the feature.
        private static Task GetOrgMembers(OrganizationId orgId)
        {
            if (orgId == OrganizationId.None) return Task.CompletedTask;
            m_OrganizationMembers ??= new Dictionary<OrganizationId, List<IUserInfo>>();
            if (m_OrganizationMembers.ContainsKey(orgId)) return Task.CompletedTask;
            m_MemberFetches ??= new Dictionary<OrganizationId, Task>();
            if (m_MemberFetches.TryGetValue(orgId, out var inFlight)) return inFlight;

            var fetch = FetchOrgMembers(orgId);
            if (!fetch.IsCompleted)
            {
                m_MemberFetches[orgId] = fetch;
            }
            return fetch;
        }

        private static async Task FetchOrgMembers(OrganizationId orgId)
        {
            try
            {
                if (m_AllOrganizations == null || !m_AllOrganizations.TryGetValue(orgId, out var organization)) return;
                var members = new List<IUserInfo>();
                await foreach (var member in organization.ListMembersAsync(Range.All, CancellationToken.None))
                {
                    members.Add(member);
                }
                m_OrganizationMembers[orgId] = members;
            }
            finally
            {
                m_MemberFetches?.Remove(orgId);
            }
        }
        
        private static List<IUserInfo> ReturnSuggestedNames(OrganizationId organizationId, string searchName)
        {
            if (m_OrganizationMembers != null && m_OrganizationMembers.TryGetValue(organizationId, out var members))
            {
                var ids = m_OrganizationMembers[organizationId];
                if (ids.Count == 0) return null;
                var users = members.Where(x => x.Name.Contains(searchName, StringComparison.OrdinalIgnoreCase));
                var firstTenName = users.Take(Mathf.Min(10, users.Count())).ToList();
                return firstTenName;
            }
            return null;
        }
        
        public static void OnOrganizationsLoaded(List<IOrganization> list)
        {
            m_AllOrganizations ??= new Dictionary<OrganizationId, IOrganization>();
            m_AllOrganizations.Clear();
            foreach (var org in list)
            {
                m_AllOrganizations.Add(org.Id, org);
            }
        }
        
        public static void CheckValidInput(TextArea textArea, GridView gridView, VisualElement sendIconButton)
        {
            bool invalid = !CheckTextAreaValidity(textArea);
            var existing = gridView.itemsSource as List<Attachment>;
            bool hasAttachment = existing != null && existing.Count > 0;
            gridView.style.display = hasAttachment? DisplayStyle.Flex: DisplayStyle.None;
            if (NetworkDetector.IsOffline)
            {
                sendIconButton.SetEnabled(false);
                return;
            }
            if (hasAttachment)
            {
                sendIconButton.SetEnabled(true);
            } else if (invalid)
            {
                sendIconButton.SetEnabled(false);
            }
            else
            {
                sendIconButton.SetEnabled(true);
            }
        }

        private static bool CheckTextAreaValidity(TextArea textArea)
        {
            var value = textArea.value;
            bool validity = !(string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value) || value.Length > textArea.maxLength);
            return validity;
        }
        
        public static string ReturnInitials(string nameLabelText)
        {
            if (string.IsNullOrWhiteSpace(nameLabelText))
                return "";

            var parts = nameLabelText.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    
            if (parts.Length == 0)
                return "";
    
            if (parts.Length == 1)
                return parts[0][0].ToString().ToUpper();
    
            return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
        }
        
        public static Color GetRandomBackgroundColorAsUnityColor(string randomColorSeed = null)
        {
            string hexColor = GetRandomBackgroundColor(randomColorSeed);
            if (ColorUtility.TryParseHtmlString(hexColor, out Color color))
            {
                return color;
            }
            return Color.white; // fallback
        }
        
        private static string GetRandomBackgroundColor(string randomColorSeed = null)
        {
            if (string.IsNullOrEmpty(randomColorSeed))
            {
                return Colors[0];
            }

            /*int lastCharIndex = randomColorSeed.Length - 1;
            int lastCharCode = (int)randomColorSeed[lastCharIndex];
            int colorIndex = lastCharCode % Colors.Length;*/
            
            // Use hash of entire string instead of just last character
            int hash = randomColorSeed.GetHashCode();
            int colorIndex = Math.Abs(hash) % Colors.Length;

            return Colors[colorIndex];
        }

        public static bool IsSupportedImageFormat(string filePath)
        {
            var extension = System.IO.Path.GetExtension(filePath)?.ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => true,
                ".png" => true,
                ".bmp" => true,
                ".tga" => true,
                ".tiff" or ".tif" => true,
                ".gif" => false,  // Unity loads only first frame
                ".webp" => false, // Not natively supported
                ".svg" => false,  // Not supported
                _ => false
            };
        }
    }
}
