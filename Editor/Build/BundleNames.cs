using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.Build
{
    // Intermediate bundle names.
    internal static class BundleNames
    {
        public static readonly GroupKey MonoScriptGroupKey = (GroupKey) "UnityMonoScripts";
        public static readonly GroupKey BuiltInShadersGroupKey = (GroupKey) "UnityBuiltInShaders";
        public static string MonoScriptBundleName => AssetBundleId.MonoScript.Name() + "_UnityMonoScripts";
        public static string BuiltInShadersBundleName => AssetBundleId.BuiltInShaders.Name() + "_UnityBuiltInShaders";

        public static string Format(AssetBundleId bundleId, GroupKey groupKey)
        {
            return bundleId.Name() + "_" + groupKey;
        }

        public static GroupKey ParseGroupKey(string bundleName)
        {
            var index = bundleName.IndexOf('_');
            Assert.IsTrue(index >= -1, "Bundle name does not contain a group key.");
            return (GroupKey) bundleName[(index + 1)..];
        }
    }
}