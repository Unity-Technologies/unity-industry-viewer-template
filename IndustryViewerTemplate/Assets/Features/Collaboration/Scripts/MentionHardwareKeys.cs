#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Unity.Industry.Viewer.Collaboration
{
    /// <summary>
    /// Bridge to the iOS native plugin (Plugins/iOS/MentionHardwareKeys.mm) that observes
    /// hardware keyboard arrow keys via the GameController framework. iOS never routes
    /// hardware key events into Unity — neither UI Toolkit nor the Input System sees them
    /// (input arrives only through the native text session) — so this is the only way to
    /// drive the mention suggestion list from an iPad keyboard cover. Observe-only: the
    /// native side cannot consume keys, so text input is never disturbed. The native
    /// handler sets pressed flags; the per-frame popover poll consumes them here.
    /// Everywhere except iOS devices the members are inert stubs.
    /// </summary>
    internal static class MentionHardwareKeys
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void MentionKeys_Start();
        [DllImport("__Internal")] private static extern int MentionKeys_ConsumeUp();
        [DllImport("__Internal")] private static extern int MentionKeys_ConsumeDown();
        [DllImport("__Internal")] private static extern int MentionKeys_HasKeyboard();

        public static void Initialize() => MentionKeys_Start();
        public static bool ConsumeUpPressed() => MentionKeys_ConsumeUp() != 0;
        public static bool ConsumeDownPressed() => MentionKeys_ConsumeDown() != 0;
        public static bool HasHardwareKeyboard => MentionKeys_HasKeyboard() != 0;
#else
        public static void Initialize() { }
        public static bool ConsumeUpPressed() => false;
        public static bool ConsumeDownPressed() => false;
        public static bool HasHardwareKeyboard => false;
#endif
    }
}
