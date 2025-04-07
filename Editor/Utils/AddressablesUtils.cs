using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    public static class AddressablesUtils
    {
        internal static T Load<T>(AssetGUID guid) where T : Object
        {
            var path = AssetDatabase.GUIDToAssetPath(guid.Value);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        internal static bool StartsWithOrdinal(this string str1, string str2)
        {
            return str1.StartsWith(str2, System.StringComparison.Ordinal);
        }
    }
}