using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Directory = System.IO.Directory;
using SearchOption = System.IO.SearchOption;

namespace Bundles.Editor
{
    public static class BundlesUtils
    {
        internal static bool StartsWithOrdinal(this string str1, string str2)
        {
            return str1.StartsWith(str2, StringComparison.Ordinal);
        }

        internal static void ReplaceFile(string src, string dst)
        {
            if (File.Exists(dst))
                File.Delete(dst);
            File.Copy(src, dst);
        }

        internal static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }

        private static bool CleanUpDirectory(string path)
        {
            if (!Directory.Exists(path)) return false;
            Directory.Delete(path, true);
            Directory.CreateDirectory(path);
            return true;
        }

        internal static void InvokeAllMethodsWithAttribute<T>(params object[] parameters) where T : Attribute
        {
            var methods = TypeCache.GetMethodsWithAttribute<T>();
            foreach (var method in methods)
            {
                L.I("[BundlesUtils] InvokeAllMethodsWithAttribute: " + method.Name);
                method.Invoke(null, parameters);
            }
        }

        private static void Menu_RegisterSelect(bool bakeCode)
        {
            var catalog = AssetCatalog.Default;
            var groups = catalog.Groups.Where(x => !x.IsGenerated).ToArray();
            var options = groups.Select(x => x.Key.Value).ToArray();
            var assetGUIDs = Selection.assetGUIDs.Distinct().Select(x => new GUID(x)).ToArray();
            SingleSelectionWindow.Prompt("Register Selection", options)
                .ContinueWith(t =>
                {
                    if (t.Result.TryGetValue(out var selection) is false) return;
                    catalog.AddEntries(groups[selection], assetGUIDs);
                    if (bakeCode) AssetCatalog.Default.BakeGroupsAndCode();
                });
        }

        [MenuItem("Assets/Bundles/Register Selection")]
        private static void Menu_RegisterSelect() => Menu_RegisterSelect(bakeCode: false);
        [MenuItem("Assets/Bundles/Register Selection (With Baking)")]
        private static void Menu_RegisterSelectWithGeneration() => Menu_RegisterSelect(bakeCode: true);

        /// <summary>
        /// Used during the build to check for unsaved scenes and provide a user popup if there are any.
        /// </summary>
        /// <returns>True if there were no unsaved scenes, or if user hits "Save and Continue" on popup.
        /// False if any scenes were unsaved, and user hits "Cancel" on popup.</returns>
        public static bool CheckModifiedScenesAndAskToSave()
        {
            var dirtyScenes = new List<Scene>();
            for (var i = 0; i < SceneManager.sceneCount; ++i)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty) dirtyScenes.Add(scene);
            }
            if (dirtyScenes.Count == 0)
                return true;

            if (EditorUtility.DisplayDialog(
                    "Unsaved Scenes",
                    "Modified Scenes must be saved to continue.",
                    "Save and Continue", "Cancel") is false)
            {
                return false;
            }

            EditorSceneManager.SaveScenes(dirtyScenes.ToArray());
            return true;
        }

        public static void TryCopyToPlatformProject(BuildTarget buildTarget, string buildPath, string projDir)
        {
            L.I("[BundlesUtils] TryCopyToPlatformProject");

            var dstDir = ResolveDstDir(projDir, buildTarget);
            if (!CleanUpDirectory(dstDir))
            {
                L.W("[BundlesUtils] Destination directory does not exist, skip copying files: " + dstDir);
                return;
            }

            var files = Directory.GetFiles(buildPath, "*", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                // Ignore hidden files and json files.
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith('.')
                    || fileName == "buildlogtep.json"
                    || fileName == "link.xml")
                {
                    continue;
                }

                File.Copy(file, Path.Combine(dstDir, fileName));
            }

            L.I($"[BundlesUtils] Copy {files.Length} files to {dstDir}");
            return;

            static string ResolveDstDir(string projDir, BuildTarget buildTarget)
            {
                var subDirs = buildTarget switch
                {
                    BuildTarget.Android => "/unityLibrary/src/main/assets/",
                    BuildTarget.iOS => "/Data/Raw/",
                    _ => throw new ArgumentOutOfRangeException(nameof(buildTarget), buildTarget, null)
                };
                return projDir + subDirs + Paths.AA;
            }
        }
    }
}