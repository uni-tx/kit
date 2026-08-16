using System.IO;
using UnityEditor;
using UnityEngine;

namespace UniTx.Serialization.Editor
{
    /// <summary>
    /// Editor menu helpers for inspecting and clearing save data.
    /// </summary>
    internal static class SerialisationMenu
    {
        private static string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, "Saves");

        /// <summary>
        /// Reveals the save directory in the OS file browser.
        /// </summary>
        [MenuItem("UniTx/Saves/Open Save Folder")]
        public static void OpenSaves()
        {
            Directory.CreateDirectory(SaveDirectoryPath);

            // Handles the platform differences (Explorer, Finder, xdg-open) that a raw
            // Process.Start with UseShellExecute does not.
            EditorUtility.RevealInFinder(SaveDirectoryPath);
        }

        /// <summary>
        /// Deletes every UniTx save file, after confirmation.
        /// </summary>
        [MenuItem("UniTx/Saves/Delete Save Files")]
        public static void ClearSaves()
        {
            if (!Directory.Exists(SaveDirectoryPath))
            {
                EditorUtility.DisplayDialog("UniTx", "There are no save files to delete.", "OK");
                return;
            }

            var count = Directory.GetFiles(SaveDirectoryPath).Length;

            // Confirm first: this is irreversible, and the old version also wiped every
            // PlayerPrefs key in the project without asking.
            if (!EditorUtility.DisplayDialog(
                    "Delete UniTx save files?",
                    $"This permanently deletes {count} file(s) in:\n{SaveDirectoryPath}\n\nThis cannot be undone.",
                    "Delete", "Cancel"))
            {
                return;
            }

            Directory.Delete(SaveDirectoryPath, recursive: true);
            Debug.Log($"[UniTx] Deleted {count} save file(s) from {SaveDirectoryPath}.");
        }

        /// <summary>
        /// Deletes every PlayerPrefs key for this project, after confirmation.
        /// </summary>
        [MenuItem("UniTx/Saves/Delete PlayerPrefs")]
        public static void ClearPlayerPrefs()
        {
            // Deliberately a separate menu item: PlayerPrefs holds settings written by Unity
            // and third-party SDKs, not just UniTx saves, so wiping it is a distinct choice.
            if (!EditorUtility.DisplayDialog(
                    "Delete all PlayerPrefs?",
                    "This clears every PlayerPrefs key for this project, including keys written " +
                    "by Unity and third-party SDKs.\n\nThis cannot be undone.",
                    "Delete", "Cancel"))
            {
                return;
            }

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[UniTx] PlayerPrefs cleared.");
        }
    }
}
