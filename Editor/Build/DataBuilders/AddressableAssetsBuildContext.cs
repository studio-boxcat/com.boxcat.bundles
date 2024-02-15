using System;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine.AddressableAssets;

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
        private AddressableAssetSettings m_Settings;
        private string m_SettingsAssetPath;

        /// <summary>
        /// The settings object to use.
        /// </summary>
        public AddressableAssetSettings Settings
        {
            get
            {
                if (m_Settings == null && !string.IsNullOrEmpty(m_SettingsAssetPath))
                    m_Settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(m_SettingsAssetPath);
                return m_Settings;
            }
            set
            {
                m_Settings = value;
                string guid;
                if (m_Settings != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m_Settings, out guid, out long _))
                    m_SettingsAssetPath = AssetDatabase.GUIDToAssetPath(guid);
                else
                    m_SettingsAssetPath = null;
            }
        }

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
    }
}