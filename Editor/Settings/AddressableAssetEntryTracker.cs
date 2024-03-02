using System.Collections.Generic;

namespace UnityEditor.AddressableAssets.Settings
{
    public class AddressableAssetEntryTracker : AssetPostprocessor
    {
        static readonly Dictionary<string, AddressableAssetEntry> _dict = new();

        public static void Track(AddressableAssetEntry entry)
        {
            var path = AssetDatabase.GUIDToAssetPath(entry.guid.Value);
            _dict.Add(path, entry);
            entry.ResetAssetPath(path);
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // Refresh tracking for moved assets.
            for (var index = 0; index < movedFromAssetPaths.Length; index++)
            {
                var orgPath = movedFromAssetPaths[index];
                if (_dict.Remove(orgPath, out var entry) is false) continue;
                var newPath = movedAssets[index];
                _dict.Add(newPath, entry);
                entry.ResetAssetPath(newPath);
            }

            // Delete entry for deleted assets.
            foreach (var path in deletedAssets)
            {
                if (_dict.Remove(path, out var entry))
                    entry.parentGroup.RemoveAssetEntry(entry);
            }
        }
    }
}