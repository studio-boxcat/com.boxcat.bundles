using System;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets
{
    internal static class AddressableAssetUtility
    {
        internal static bool IsInResources(string path)
        {
            return path.Contains("/Resources/", StringComparison.Ordinal);
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
    }
}