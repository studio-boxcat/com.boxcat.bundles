#if UNITY_EDITOR
using System;
using JetBrains.Annotations;
using UnityEditor;

namespace UnityEngine.AddressableAssets
{
    [AttributeUsage(AttributeTargets.Method)]
    public class EditorAddressablesImplFactoryAttribute : Attribute
    {
    }

    public static class EditorAddressablesImplFactory
    {
        // To keep state after compilation, use SessionState instead of static bool.
        static string _argument
        {
            get => SessionState.GetString("EditorAddressablesImplFactory_Argument", "");
            set
            {
                SessionState.SetString("EditorAddressablesImplFactory_Argument", value);
                Addressables.Purge();
            }
        }

        internal static void UseCatalog() => _argument = "CATALOG";
        internal static void UseAssetDatabase(Object catalog) => _argument = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(catalog));

        [CanBeNull]
        internal static IAddressablesImpl Create()
        {
            // If not playing, always use AssetDatabase.
            if (EditorApplication.isPlaying is false)
                return CreateEditorImpl(null);

            // If playing, use the argument set by UseCatalog or UseAssetDatabase.
            var argument = _argument;
            return argument != "CATALOG" ? CreateEditorImpl(argument) : null;

            static IAddressablesImpl CreateEditorImpl(string guid)
            {
                var method = TypeCache.GetMethodsWithAttribute<EditorAddressablesImplFactoryAttribute>()[0];
                var argumentObj = string.IsNullOrEmpty(guid) is false
                    ? AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid))
                    : null; // Use default catalog asset.
                return (IAddressablesImpl) method.Invoke(null, new object[] { argumentObj });
            }
        }
    }
}
#endif