using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.Settings
{
    using Object = UnityEngine.Object;

    internal static class AddressableAssetUtility
    {
        internal static bool IsInResources(string path)
        {
            return path.Contains("/Resources/", StringComparison.Ordinal);
        }

        internal static bool TryGetPathAndGUIDFromTarget(Object target, out string path, out string guid)
        {
            if (target == null)
            {
                guid = string.Empty;
                path = string.Empty;
                return false;
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out guid, out long _))
            {
                guid = string.Empty;
                path = string.Empty;
                return false;
            }

            path = AssetDatabase.GetAssetOrScenePath(target);
            return IsPathValidForEntry(path);
        }

        static readonly string[] _excludedExtensions = { ".cs", ".js", ".boo", ".exe", ".dll", ".meta", ".preset", ".asmdef" };

        internal static bool IsPathValidForEntry(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            Assert.IsFalse(path.Contains('\\'), "Path contains '\\' - not a valid path for AddressableAssetEntry: " + path);

            // Exclude files that are not in the Assets or Packages folder
            if (!path.StartsWith("Assets/", StringComparison.Ordinal)
                && !path.StartsWith("Packages/", StringComparison.Ordinal))
                return false;

            // Exclude Editor and Gizmos folders
            if (path.Contains("/Editor/", StringComparison.Ordinal)
                || path.Contains("/Gizmos/", StringComparison.Ordinal))
                return false;

            // Exclude files with excluded extensions
            if (IsExcludedExtension(path))
                return false;

            return true;

            static bool IsExcludedExtension(string path)
            {
                foreach (var ext in _excludedExtensions)
                {
                    if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
        }

        internal static void MoveEntriesToGroup(AddressableAssetSettings settings, List<AddressableAssetEntry> entries, AddressableAssetGroup group)
        {
            foreach (var entry in entries)
            {
                if (entry.parentGroup != group)
                    AddressableAssetSettings.MoveEntry(entry, group);
            }
        }
    }
}