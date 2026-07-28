#if UNITY_STANDALONE || UNITY_EDITOR
using SFB;
#endif
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Linq;
using System.Collections.Generic;

namespace Unity.Industry.Viewer.Shared
{
    public static class FileBrowser
    {
        public const string SupportedStreamingFileExtensions =
            "pxz,3ds,sat,sab,dwg,dxf,fbx,ipt,iam,nwd,nwc,rvt,rfa,catpart,catproduct,catshape,cgr,3dxml,dae" +
            ",asm,neu,prt,xas,xpr,pvs,pvz,gltf,glb,gds,ifc,igs,iges,obj,plmxml,prc,3dm,rvm,par,pwd,psm" +
            ",sldasm,sldprt,stp,step,stpz,stepz,stpx,stpxz,stl,vda,wrl,vrml";

        public const string DefaultImageFileExtensions = "png,jpg,jpeg,bmp,tiff,tif,gif";

        private static HashSet<string> m_SupportedStreamingFileExtensionsHashSet =
            SupportedStreamingFileExtensions.Split(',').ToHashSet();

        private static HashSet<string> m_DefaultImageFileExtensionsHashSet =
            DefaultImageFileExtensions.Split(',').ToHashSet();

        private static string PrepareFileExtensionForComparison(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return string.Empty;
            }

            return extension.Trim(' ', '*', '.').ToLowerInvariant();
        }

        public static bool IsSupportedStreamingFileExtension(string extension)
        {
            return m_SupportedStreamingFileExtensionsHashSet.Contains(PrepareFileExtensionForComparison(extension));
        }

        public static bool IsDefaultImageFileExtension(string extension)
        {
            return m_DefaultImageFileExtensionsHashSet.Contains(PrepareFileExtensionForComparison(extension));
        }

        // Native file dialogs disagree on how they report a cancelled/empty selection: the Editor and
        // Windows backends return a zero-length array, but the macOS/Linux backends split an empty
        // native string and hand back a single empty entry ([""]). Strip null/empty/whitespace paths
        // so callers reliably get an empty array on cancel and never create a phantom (empty-path) entry.
        private static string[] SanitizePaths(string[] paths)
        {
            return paths == null
                ? Array.Empty<string>()
                : paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        }

#if UNITY_STANDALONE || UNITY_EDITOR
        // Builds the SFB desktop extension filter from a comma-separated list (null when none given).
        private static ExtensionFilter[] BuildDesktopFilter(string extension)
        {
            return string.IsNullOrEmpty(extension)
                ? null
                : new[] { new ExtensionFilter("Supported Files", extension.Split(',')) };
        }
#endif

#if UNITY_IOS || UNITY_ANDROID
        // Converts a comma-separated extension list to native mobile file types. Custom extensions are
        // supported on iOS only; Android ignores them, so this returns null there.
        // https://github.com/yasirkula/UnityNativeFilePicker?tab=readme-ov-file#unity-native-file-picker-plugin
        private static string[] BuildMobileFileTypes(string extension)
        {
#if UNITY_IOS
            if (!string.IsNullOrEmpty(extension))
            {
                return extension.Split(",")
                    .Select(ext => NativeFilePicker.ConvertExtensionToFileType(ext))
                    .ToArray();
            }
#endif
            return null;
        }
#endif


        public static void OpenFile(string title, string defaultFolder, string extension, Action<string> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

#if UNITY_EDITOR
            callback(EditorUtility.OpenFilePanel(title, defaultFolder, extension));
            return;
#endif

#if UNITY_STANDALONE// || UNITY_WEBGL
            var convertedExtensions = BuildDesktopFilter(extension);

            StandaloneFileBrowser.OpenFilePanelAsync(title, defaultFolder, convertedExtensions, false, (paths) =>
            {
                callback(SanitizePaths(paths).FirstOrDefault() ?? string.Empty);
            });

            return;
#endif

            
#if UNITY_IOS || UNITY_ANDROID
            if( NativeFilePicker.IsFilePickerBusy() )
                return;
            var convertedMobileExtensions = BuildMobileFileTypes(extension);
            NativeFilePicker.PickFile((path) => { callback(path); }, convertedMobileExtensions);
            return;
#endif

            throw new PlatformNotSupportedException("File browser is not supported on this platform.");
        }

        public static void OpenFiles(string title, string defaultFolder, string extension, Action<string[]> callback)
        {
            if(callback == null) throw new ArgumentNullException(nameof(callback));
            
#if UNITY_EDITOR || UNITY_STANDALONE
            var convertedExtensions = BuildDesktopFilter(extension);

            StandaloneFileBrowser.OpenFilePanelAsync(title, defaultFolder, convertedExtensions, true,
                (paths) => callback(SanitizePaths(paths)));
            return;
#endif
            
            
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            if( NativeFilePicker.IsFilePickerBusy() )
                return;
            var convertedMobileExtensions = BuildMobileFileTypes(extension);

            NativeFilePicker.PickMultipleFiles((files) => { callback(SanitizePaths(files)); }, convertedMobileExtensions);
            return;
#endif
            
            throw new PlatformNotSupportedException("File browser is not supported on this platform.");
        }

        /* Summary: Opens a save file dialog.
         * title: The title of the dialog.
         * defaultFolder: The default folder to open.
         * fileNameWithExtension: The default file name with extension.
         * extension: The extension filter (e.g. "png", "jpg", "txt").
         * callback: The callback to invoke with the selected file path or an empty string if cancelled.
         */
#if UNITY_EDITOR ||UNITY_STANDALONE// || UNITY_WEBGL
        public static void SaveFile(string title, string defaultFolder, string fileNameWithExtension, string extension,
            Action<string> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
#if UNITY_EDITOR
            callback(EditorUtility.SaveFilePanel(title, defaultFolder, fileNameWithExtension, extension));
            return;
#endif
            
#if UNITY_STANDALONE// || UNITY_WEBGL
            StandaloneFileBrowser.SaveFilePanelAsync(title, defaultFolder, fileNameWithExtension, extension, callback);
            return;
#endif
        }
#endif

#if UNITY_IOS || UNITY_ANDROID
        public static void ExportFile(string filePath, Action<bool> callback)
        {
            NativeFilePicker.ExportFile(filePath, ExportCallback);
            return;

            void ExportCallback(bool success)
            {
                callback(success);
            }
        }
#endif
    }
}
