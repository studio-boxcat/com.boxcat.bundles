using System;
using System.Collections.Generic;
using UnityEditor.Build.Pipeline.Interfaces;

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
        /// The time the build started
        /// </summary>
        public DateTime buildStartTime;

        /// <summary>
        /// A mapping of Asset GUID's to resulting ResourceLocation entries.
        /// </summary>
        internal Dictionary<AssetGUID, EntryDef> entries;

        public Dictionary<BundleKey, AssetGroup> bundleToAssetGroup;

        /// <summary>
        /// Mapping of AssetBundle to the direct dependencies.
        /// </summary>
        public Dictionary<BundleKey, HashSet<BundleKey>> bundleToImmediateBundleDependencies;

        /// <summary>
        /// A mapping of AssetBundle to the full dependency tree, flattened into a single list.
        /// </summary>
        public Dictionary<BundleKey, HashSet<BundleKey>> bundleToExpandedBundleDependencies;


        public AddressableAssetsBuildContext(AddressableCatalog catalog)
        {
            Catalog = catalog;
        }
    }
}