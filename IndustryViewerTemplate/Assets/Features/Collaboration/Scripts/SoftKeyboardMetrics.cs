using UnityEngine;

namespace Unity.Industry.Viewer.Collaboration
{
    /// <summary>
    /// Cross-platform height of the on-screen keyboard, in screen pixels (0 when hidden).
    /// iOS reports it through TouchScreenKeyboard.area; on Android that API is documented
    /// to return an empty rect, so the height is measured instead as the part of the
    /// activity's decor view that the keyboard obscures (getWindowVisibleDisplayFrame).
    /// </summary>
    internal static class SoftKeyboardMetrics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Reused across calls to avoid per-poll JNI allocations.
        private static AndroidJavaObject s_DecorView;
        private static AndroidJavaObject s_VisibleFrame;

        public static float GetHeightPixels()
        {
            if (!TouchScreenKeyboard.visible) return 0f;
            try
            {
                if (s_DecorView == null)
                {
                    using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    using var window = activity.Call<AndroidJavaObject>("getWindow");
                    s_DecorView = window.Call<AndroidJavaObject>("getDecorView");
                    s_VisibleFrame = new AndroidJavaObject("android.graphics.Rect");
                }

                s_DecorView.Call("getWindowVisibleDisplayFrame", s_VisibleFrame);
                float height = Screen.height - s_VisibleFrame.Get<int>("bottom");
                // Coordinate spaces of the surface and the window can differ by system-bar
                // heights; anything smaller than a plausible keyboard is such an artifact.
                // The floor is density-scaled (system bars stay under ~56dp, docked
                // keyboards exceed ~72dp even in compact or split layouts) rather than a
                // fraction of Screen.height, which overshoots compact keyboards on tall
                // screens and in multi-window. Floating keyboards do not resize the
                // window at all and therefore cannot be measured by this method.
                float density = Screen.dpi > 0f ? Screen.dpi / 160f : 2f;
                return height >= 72f * density ? height : 0f;
            }
            catch
            {
                // Unexpected player/activity variants — or a decor view from a RECREATED
                // activity (config change): drop the cache so the next call re-resolves
                // instead of failing forever, and degrade to "no keyboard measured".
                s_DecorView?.Dispose();
                s_DecorView = null;
                s_VisibleFrame?.Dispose();
                s_VisibleFrame = null;
                return 0f;
            }
        }
#else
        public static float GetHeightPixels()
        {
            return TouchScreenKeyboard.visible ? TouchScreenKeyboard.area.height : 0f;
        }
#endif
    }
}
