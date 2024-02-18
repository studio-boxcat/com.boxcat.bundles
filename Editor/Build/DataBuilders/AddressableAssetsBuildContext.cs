using System;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Interface for any Addressables specific context objects to be used in the Scriptable Build Pipeline context store
    /// </summary>
    public interface IAddressableAssetsBuildContext : IContextObject
    {
    }

    /// <summary>
    /// Simple context object for passing data through SBP, between different sections of Addressables code.
    /// </summary>
    public class AddressableAssetsBuildContext : IAddressableAssetsBuildContext
    {
        /// <summary>
        /// The settings object to use.
        /// </summary>
        public readonly AddressableAssetSettings Settings;

        /// <summary>
        /// The time the build started
        /// </summary>
        public DateTime buildStartTime;

        /// <summary>
        /// A mapping of Asset GUID's to resulting ResourceLocation entries.
        /// </summary>
        internal Dictionary<AssetGUID, EntryDef> entries;

        public Dictionary<BundleKey, AddressableAssetGroup> bundleToAssetGroup;

        /// <summary>
        /// Mapping of AssetBundle to the direct dependencies.
        /// </summary>
        public Dictionary<BundleKey, HashSet<BundleKey>> bundleToImmediateBundleDependencies;

        /// <summary>
        /// A mapping of AssetBundle to the full dependency tree, flattened into a single list.
        /// </summary>
        public Dictionary<BundleKey, HashSet<BundleKey>> bundleToExpandedBundleDependencies;


        public AddressableAssetsBuildContext(AddressableAssetSettings settings)
        {
            Settings = settings;
        }
    }
}