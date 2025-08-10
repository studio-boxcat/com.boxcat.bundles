using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Pipeline.Interfaces;

namespace Bundles.Editor
{
    /// <summary>
    /// Simple context object for passing data through SBP, between different sections of Bundles code.
    /// </summary>
    internal class BundlesBuildContext : IContextObject
    {
        /// <summary>
        /// The catalog object to use.
        /// </summary>
        public readonly AssetCatalog Catalog;

        /// <summary>
        /// A mapping of Asset GUID's to resulting ResourceLocation entries.
        /// </summary>
        internal Dictionary<GUID, EntryDef> entries;

        /// <summary>
        /// Mapping of AssetBundle to the direct dependencies.
        /// </summary>
        public Dictionary<AssetBundleId, HashSet<AssetBundleId>> bundleToImmediateBundleDependencies;

        /// <summary>
        /// A mapping of AssetBundle to the full dependency tree, flattened into a single list.
        /// </summary>
        public Dictionary<AssetBundleId, HashSet<AssetBundleId>> bundleToExpandedBundleDependencies;


        public BundlesBuildContext(AssetCatalog catalog)
        {
            Catalog = catalog;
        }
    }
}