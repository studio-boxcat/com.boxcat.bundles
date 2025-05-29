#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Bundles
{
    public readonly struct AssetGroupGenerationDef
    {
        [CanBeNull] public readonly string GroupName;
        public readonly (string Address, string Path)[] Assets;
        public readonly byte? BundleMinor; // BundleMajor + BundleMinor = BundleId

        public AssetGroupGenerationDef(string groupName, byte bundleMinor, params (uint Address, string Path)[] assets)
        {
            GroupName = groupName;
            Assets = assets.Select(x => (x.Address.ToString(), x.Path)).ToArray();
            BundleMinor = bundleMinor;
        }

        public AssetGroupGenerationDef(string groupName, byte bundleMinor, params string[] assetPaths)
            : this(groupName, bundleMinor, assetPaths.Select((x, i) => ((uint) i, x)).ToArray()) { }

        public AssetGroupGenerationDef(string groupName, byte bundleMinor, IEnumerable<string> assetPaths)
            : this(groupName, bundleMinor, assetPaths.ToArray()) { }

        public AssetGroupGenerationDef(string groupName, byte bundleMinor, params Object[] assets)
            : this(groupName, bundleMinor, assets.Select(AssetDatabase.GetAssetPath)) { }

        public AssetGroupGenerationDef(byte bundleMinor, params Object[] assets)
            : this(null, bundleMinor, assets.Select(AssetDatabase.GetAssetPath)) { }
    }

    [AttributeUsage(AttributeTargets.Method), MeansImplicitUse]
    public class AssetGroupGeneratorAttribute : Attribute
    {
        public readonly AssetBundleMajor? BundleMajor; // if set, can access bundle directly

        public AssetGroupGeneratorAttribute(AssetBundleMajor bundleMajor)
        {
            BundleMajor = bundleMajor;
        }

        [RequiredSignature, UsedImplicitly]
        private static IEnumerable<AssetGroupGenerationDef> Signature() => null;
    }
}
#endif