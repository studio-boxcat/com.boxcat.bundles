using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets
{
    public partial class AddressableCatalog : ScriptableObject
    {
        [FormerlySerializedAs("AssetGroups")]
        [SerializeField, HideInInspector]
        public AssetGroup[] Groups;

        internal AssetBundleBuild[] GenerateBundleBuilds()
        {
            var builds = new AssetBundleBuild[Groups.Length];
            for (var i = 0; i < Groups.Length; i++)
            {
                var build = Groups[i].GenerateAssetBundleBuild();
                L.I(string.Format(
                    $"[AddressableCatalog] Build {build.assetBundleName}:\n"
                    + string.Join("\n", build.addressableNames.Select((x, i) => $"{x} -> {build.assetNames[i]}"))));
                builds[i] = build;
            }
            return builds;
        }
    }
}