#if UNITY_STANDALONE_WIN

using System;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;
#if UNITY_2023_2_0 || UNITY_2023_2_1 || UNITY_2023_2_2 || UNITY_2023_2_3 || UNITY_2023_2_4 || UNITY_2023_2_5 || UNITY_2023_2_6 || UNITY_2023_2_7 || UNITY_2023_2_8 || UNITY_2023_2_9 || UNITY_2023_2_10 || UNITY_2023_2_11 || UNITY_2023_2_12 || UNITY_2023_2_13 || UNITY_2023_2_14 || UNITY_2023_2_15 || UNITY_2023_2_16 || UNITY_2023_2_17 || UNITY_2023_2_18 || UNITY_2023_2_19
using VistaOpenFileDialog = System.Windows.Forms.OpenFileDialog;
using VistaFolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using VistaSaveFileDialog = System.Windows.Forms.SaveFileDialog;
#else
using Ookii.Dialogs.WinForms;
#endif

namespace SFB {
    // For fullscreen support
    // - WindowWrapper class and GetActiveWindow() are required for modal file dialog.
    // - "PlayerSettings/Visible In Background" should be enabled, otherwise when file dialog opened app window minimizes automatically.

    public class WindowWrapper : IWin32Window {
        private IntPtr _hwnd;
        public WindowWrapper(IntPtr handle) { _hwnd = handle; }
        public IntPtr Handle { get { return _hwnd; } }
    }

    public class StandaloneFileBrowserWindows : IStandaloneFileBrowser {
        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        public string[] OpenFilePanel(string title, string directory, ExtensionFilter[] extensions, bool multiselect) {
            var fd = new VistaOpenFileDialog();
            fd.Title = title;
            if (extensions != null) {
                fd.Filter = GetFilterFromFileExtensionList(extensions);
                fd.FilterIndex = 1;
            }
            else {
                fd.Filter = string.Empty;
            }
            fd.Multiselect = multiselect;
            if (!string.IsNullOrEmpty(directory)) {
                fd.FileName = GetDirectoryPath(directory);
            }
            var res = fd.ShowDialog(new WindowWrapper(GetActiveWindow()));
            var filenames = res == DialogResult.OK ? fd.FileNames : new string[0];
            fd.Dispose();
            return filenames;
        }

        public void OpenFilePanelAsync(string title, string directory, ExtensionFilter[] extensions, bool multiselect, Action<string[]> cb) {
            cb.Invoke(OpenFilePanel(title, directory, extensions, multiselect));
        }

        public string[] OpenFolderPanel(string title, string directory, bool multiselect) {
            var fd = new VistaFolderBrowserDialog();
            fd.Description = title;
            if (!string.IsNullOrEmpty(directory)) {
                fd.SelectedPath = GetDirectoryPath(directory);
            }
            var res = fd.ShowDialog(new WindowWrapper(GetActiveWindow()));
            var filenames = res == DialogResult.OK ? new []{ fd.SelectedPath } : new string[0];
            fd.Dispose();
            return filenames;
        }

        public void OpenFolderPanelAsync(string title, string directory, bool multiselect, Action<string[]> cb) {
            cb.Invoke(OpenFolderPanel(title, directory, multiselect));
        }

        public string SaveFilePanel(string title, string directory, string defaultName, ExtensionFilter[] extensions) {
            var fd = new VistaSaveFileDialog();
            fd.Title = title;

            var finalFilename = "";

            if (!string.IsNullOrEmpty(directory)) {
                finalFilename = GetDirectoryPath(directory);
            }

            if (!string.IsNullOrEmpty(defaultName)) {
                finalFilename += defaultName;
            }

            fd.FileName = finalFilename;
            if (extensions != null) {
                fd.Filter = GetFilterFromFileExtensionList(extensions);
                fd.FilterIndex = 1;
                fd.DefaultExt = extensions[0].Extensions[0];
                fd.AddExtension = true;
            }
            else {
                fd.DefaultExt = string.Empty;
                fd.Filter = string.Empty;
                fd.AddExtension = false;
            }
            var res = fd.ShowDialog(new WindowWrapper(GetActiveWindow()));
            var filename = res == DialogResult.OK ? fd.FileName : "";
            fd.Dispose();
            return filename;
        }

        public void SaveFilePanelAsync(string title, string directory, string defaultName, ExtensionFilter[] extensions, Action<string> cb) {
            cb.Invoke(SaveFilePanel(title, directory, defaultName, extensions));
        }

        // .NET Framework FileDialog Filter format
        // https://msdn.microsoft.com/en-us/library/microsoft.win32.filedialog.filter
        private static string GetFilterFromFileExtensionList(ExtensionFilter[] extensions) {
            var filterString = "";
            foreach (var filter in extensions) {
                filterString += filter.Name + " (";

                foreach (var ext in filter.Extensions) {
                    filterString += "*." + ext + ",";
                }

                filterString = filterString.Remove(filterString.Length - 1);
                filterString += ") |";

                foreach (var ext in filter.Extensions) {
                    filterString += "*." + ext + "; ";
                }

                filterString += "|";
            }
            filterString = filterString.Remove(filterString.Length - 1);
            return filterString;
        }

        private static string GetDirectoryPath(string directory) {
            var directoryPath = Path.GetFullPath(directory);
            if (!directoryPath.EndsWith("\\")) {
                directoryPath += "\\";
            }
            if (Path.GetPathRoot(directoryPath) == directoryPath) {
                return directory;
            }
            return Path.GetDirectoryName(directoryPath) + Path.DirectorySeparatorChar;
        }
    }
}

#endif
