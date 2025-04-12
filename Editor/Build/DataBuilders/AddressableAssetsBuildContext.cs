using System.Collections.Generic;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Simple context object for passing data through SBP, between different sections of Addressables code.
    /// </summary>
    internal class AddressableAssetsBuildContext : IContextObject
    {
        /// <summary>
        /// The catalog object to use.
        /// </summary>
        public readonly AddressableCatalog Catalog;

        /// <summary>
        /// A mapping of Asset GUID's to resulting ResourceLocation entries.
        /// </summary>
        internal Dictionary<AssetGUID, EntryDef> entries;

        /// <summary>
        /// Mapping of AssetBundle to the direct dependencies.
        /// </summary>
        public Dictionary<AssetBundleId, HashSet<AssetBundleId>> bundleToImmediateBundleDependencies;

        /// <summary>
        /// A mapping of AssetBundle to the full dependency tree, flattened into a single list.
        /// </summary>
        public Dictionary<AssetBundleId, HashSet<AssetBundleId>> bundleToExpandedBundleDependencies;


        public AddressableAssetsBuildContext(AddressableCatalog catalog)
        {
            Catalog = catalog;
        }
    }
}