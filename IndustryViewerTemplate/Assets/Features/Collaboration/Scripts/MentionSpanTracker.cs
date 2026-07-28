using System;
using System.Collections.Generic;
using System.Text;

namespace Unity.Industry.Viewer.Collaboration
{
    /// <summary>
    /// One confirmed @-mention inside a plain-text composer buffer.
    /// </summary>
    public class MentionSpan
    {
        public int Start;      // Index of the '@' in the plain text.
        public int Length;     // Length of the visible "@Name" text.
        public string UserId;
        public string Name;
        public int End => Start + Length;
    }

    /// <summary>
    /// Tracks @-mention spans over a plain-text composer buffer (the Slack model: the visible
    /// text stays plain, mentions live in this parallel span list). Feed every old->new text
    /// transition through <see cref="ApplyTextChange"/>: spans before the edit stay put, spans
    /// after it shift by the edit delta, and any span the edit touches dissolves back to plain
    /// text (the mention is dropped, the text remains). Because the tracker only consumes
    /// old/new string pairs it does not care whether changes arrive per keystroke (desktop
    /// changing events) or in batches (on-screen keyboards / polling).
    /// </summary>
    public class MentionSpanTracker
    {
        readonly List<MentionSpan> m_Spans = new();

        public IReadOnlyList<MentionSpan> Spans => m_Spans;

        /// <summary>
        /// Position in the new text right after the last applied edit, i.e. where the caret
        /// lands after typing. Used to bound the popover search filter without relying on
        /// TextField.cursorIndex, which can lag mid-event.
        /// </summary>
        public int LastEditEnd { get; private set; }

        /// <summary>
        /// The span dissolved by the last <see cref="ApplyTextChange"/>, when that edit
        /// dissolved exactly one; null otherwise. Lets the composer re-open the suggestion
        /// list when the user backspaces into a completed tag (the Slack behavior: the tag
        /// un-pills and the typeahead resumes on what's left after the '@').
        /// </summary>
        public MentionSpan LastDissolvedSpan { get; private set; }

        /// <summary>
        /// Input-pipeline snapshot: the composer text as last processed through the mention
        /// pipeline. Value-changing events and the mobile poll both diff against (and then
        /// update) this, so whichever sees a change first processes it and the other no-ops —
        /// which is what makes a hardware keyboard attaching/detaching mid-edit a non-event.
        /// </summary>
        public string LastObservedText { get; set; }

        public void Clear()
        {
            m_Spans.Clear();
            LastEditEnd = 0;
            LastDissolvedSpan = null;
            LastObservedText = null;
        }

        /// <summary>
        /// Registers a confirmed mention. Call after inserting the visible "@Name" text.
        /// </summary>
        public void AddSpan(int start, int length, string userId, string name)
        {
            var span = new MentionSpan { Start = start, Length = length, UserId = userId, Name = name };
            var index = m_Spans.FindIndex(s => s.Start > start);
            if (index < 0)
            {
                m_Spans.Add(span);
            }
            else
            {
                m_Spans.Insert(index, span);
            }
        }

        /// <summary>
        /// Applies a text transition to the span list. The edit is located as the single
        /// contiguous replacement between the common prefix and common suffix of the two
        /// strings. When adjacent characters are identical the located position can be off by
        /// a character or two; the worst case is a span dissolving that could have survived,
        /// which degrades safely to plain text.
        /// </summary>
        public void ApplyTextChange(string oldText, string newText)
        {
            LastDissolvedSpan = null;
            oldText ??= string.Empty;
            newText ??= string.Empty;
            if (string.Equals(oldText, newText, StringComparison.Ordinal)) return;

            int oldLength = oldText.Length;
            int newLength = newText.Length;
            int minLength = Math.Min(oldLength, newLength);

            int prefix = 0;
            while (prefix < minLength && oldText[prefix] == newText[prefix])
            {
                prefix++;
            }

            int suffix = 0;
            while (suffix < minLength - prefix
                   && oldText[oldLength - 1 - suffix] == newText[newLength - 1 - suffix])
            {
                suffix++;
            }

            int removedStart = prefix;             // Replaced region in the old text:
            int removedEnd = oldLength - suffix;   // [removedStart, removedEnd)
            int delta = newLength - oldLength;
            LastEditEnd = newLength - suffix;

            int dissolvedCount = 0;
            for (int i = m_Spans.Count - 1; i >= 0; i--)
            {
                var span = m_Spans[i];
                if (span.End <= removedStart) continue;    // Entirely before the edit.
                if (span.Start >= removedEnd)              // Entirely after: shift with the delta.
                {
                    span.Start += delta;
                    continue;
                }
                m_Spans.RemoveAt(i);                       // Edit touches the span: dissolve it.
                LastDissolvedSpan = span;
                dissolvedCount++;
            }
            if (dissolvedCount != 1)
            {
                LastDissolvedSpan = null;
            }
        }

        /// <summary>
        /// Converts the plain composer text to the cloud wire format, replacing each span's
        /// visible "@Name" with :user[Name]{#userId} (the format the annotation service parses
        /// for notifications). Spans whose text no longer matches are skipped defensively and
        /// left as plain text. The PARSE side of this format is
        /// CollaborationUIUtility.k_CloudMentionPattern — keep the two in sync; span names are
        /// pre-sanitized (CollaborationUIUtility.SanitizeMentionName) so they can never
        /// contain the ']' that terminates the pattern's name group.
        /// </summary>
        public string ToCloudFormat(string plainText)
        {
            if (string.IsNullOrEmpty(plainText) || m_Spans.Count == 0) return plainText;

            var builder = new StringBuilder(plainText.Length + m_Spans.Count * 24);
            int cursor = 0;
            foreach (var span in m_Spans)
            {
                if (span.Start < cursor || span.End > plainText.Length) continue;
                var visible = plainText.Substring(span.Start, span.Length);
                if (!string.Equals(visible, "@" + span.Name, StringComparison.Ordinal)) continue;

                builder.Append(plainText, cursor, span.Start - cursor);
                builder.Append(":user[").Append(span.Name).Append("]{#").Append(span.UserId).Append('}');
                cursor = span.End;
            }
            builder.Append(plainText, cursor, plainText.Length - cursor);
            return builder.ToString();
        }
    }
}
