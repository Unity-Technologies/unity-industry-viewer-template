using System.Collections;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Unity.Industry.Viewer.Shared
{
    public static class UIUtility
    {
        public const string k_ToastMessageName = "appui-toast__message";
        
        public static string GetTitleLocalizedStringForAppUI(this LocalizedString localizedString)
        {
            var tableEntry = LocalizationSettings.StringDatabase.GetTableEntry(localizedString.TableReference, localizedString.TableEntryReference);
            return $"@{tableEntry.Table.TableCollectionName}:{tableEntry.Entry.SharedEntry.Key}";
        }

        public static async Task<string> GetTitleLocalizedStringForAppUIAsync(this LocalizedString localizedString)
        {
            var stringTableHandle = LocalizationSettings.StringDatabase.GetTableAsync(localizedString.TableReference);
            await stringTableHandle.Task;
    
            StringTable stringTable = stringTableHandle.Result;
            if (stringTable == null)
            {
                Debug.LogWarning($"Localization table '{localizedString.TableReference}' not found.");
                return string.Empty;
            }
    
            // Pass the string key from TableEntryReference to GetEntry
            TableEntry tableEntry = stringTable.GetEntry(localizedString.TableEntryReference.KeyId);
    
            if (tableEntry == null)
            {
                Debug.LogWarning($"Table entry '{localizedString.TableEntryReference}' not found in table '{stringTable.TableCollectionName}'.");
                return string.Empty;
            }
            return $"@{stringTable.TableCollectionName}:{tableEntry.Key}";
        }

        public static bool IsDisplayOn(this VisualElement element)
        {
            return element.style.display == DisplayStyle.Flex;
        }

        public static void DisplayOn(this VisualElement element)
        {
            element.style.display = DisplayStyle.Flex;
        }

        public static void DisplayOff(this VisualElement element)
        {
            element.style.display = DisplayStyle.None;
        }

        public static void SetDisplay(this VisualElement element, bool on)
        {
            if (on) element.DisplayOn(); else element.DisplayOff();
        }

        public static void AddStyleSheetIfMissing(this VisualElement element, StyleSheet sheet)
        {
            if (!element.styleSheets.Contains(sheet))
            {
                element.styleSheets.Add(sheet);
            }
        }

        public static void RemoveStyleSheetIfPresent(this VisualElement element, StyleSheet sheet)
        {
            if (element.styleSheets.Contains(sheet))
            {
                element.styleSheets.Remove(sheet);
            }
        }

        // Workaround for a Unity bug where GameObjects are recreated when reopening a tool: toggles the
        // active EventSystem off and on across frames. Runs on a throwaway CoroutineRunner so the caller
        // need not be an active MonoBehaviour. Remove once the underlying Unity issue is fixed.
        public static void RefreshEventSystem()
        {
            GameObject workaroundObject = new GameObject("temp");
            CoroutineRunner coroutineRunner = workaroundObject.AddComponent<CoroutineRunner>();
            coroutineRunner.RunCoroutine(Refresh(), null);
            return;

            IEnumerator Refresh()
            {
                if (EventSystem.current.gameObject == null) yield break;
                yield return null;
                var eventGameObject = EventSystem.current.gameObject;
                eventGameObject?.SetActive(false);
                yield return null;
                eventGameObject?.SetActive(true);
            }
        }
    }
}
