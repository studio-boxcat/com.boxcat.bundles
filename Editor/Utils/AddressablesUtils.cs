using System;
using System.IO;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets
{
    internal static class AddressablesUtils
    {
        internal static T Load<T>(AssetGUID guid) where T : Object
        {
            var path = AssetDatabase.GUIDToAssetPath(guid.Value);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

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

        internal static void InvokeAllMethodsWithAttribute<T>(params object[] parameters) where T : Attribute
        {
            var methods = TypeCache.GetMethodsWithAttribute<T>();
            foreach (var method in methods)
            {
                L.I("[AddressablesUtils] InvokeAllMethodsWithAttribute: " + method.Name);
                method.Invoke(null, parameters);
            }
        }
    }
}