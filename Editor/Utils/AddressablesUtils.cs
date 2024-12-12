using System.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    public static class AddressablesUtils
    {
        public static bool TryGetAssetByAddress(this AddressableAssetGroup group, string name, out Object asset)
        {
            var entry = group.entries.FirstOrDefault(x => x.address == name);
            if (entry != null)
            {
                asset = entry.MainAsset;
                return true;
            }
            else
            {
                asset = default;
                return false;
            }
        }

        internal static T Load<T>(AssetGUID guid) where T : Object
        {
            var path = AssetDatabase.GUIDToAssetPath(guid.Value);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        internal static string ResolveAssetPath(this AddressableAssetEntry entry)
        {
            return AssetDatabase.GUIDToAssetPath(entry.guid.Value);
        }

        internal static bool StartsWithOrdinal(this string str1, string str2)
        {
            return str1.StartsWith(str2, System.StringComparison.Ordinal);
        }

        internal static bool EndsWithOfOrdinal(this string str, string value)
        {
            return str.EndsWith(value, System.StringComparison.Ordinal);
        }
    }
}