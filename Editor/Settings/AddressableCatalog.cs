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
                builds[i] = Groups[i].GenerateAssetBundleBuild();
            return builds;
        }
    }
}