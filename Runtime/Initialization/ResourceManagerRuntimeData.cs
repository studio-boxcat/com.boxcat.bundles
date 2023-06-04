using System;
using System.Collections.Generic;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Serialization;

namespace UnityEngine.AddressableAssets.Initialization
{
    /// <summary>
    /// Runtime data that is used to initialize the Addressables system.
    /// </summary>
    [Serializable]
    public class ResourceManagerRuntimeData
    {
        /// <summary>
        /// Address of the contained catalogs.
        /// </summary>
        public const string kCatalogAddress = "AddressablesMainContentCatalog";

        [SerializeField]
        string m_buildTarget;

        /// <summary>
        /// The name of the build target that this data was prepared for.
        /// </summary>
        public string BuildTarget
        {
            get { return m_buildTarget; }
            set { m_buildTarget = value; }
        }

        [FormerlySerializedAs("m_settingsHash")]
        [SerializeField]
        string m_SettingsHash;

        /// <summary>
        /// The hash of the settings that generated this runtime data.
        /// </summary>
        public string SettingsHash
        {
            get { return m_SettingsHash; }
            set { m_SettingsHash = value; }
        }

        [FormerlySerializedAs("m_catalogLocations")]
        [SerializeField]
        List<ResourceLocationData> m_CatalogLocations = new List<ResourceLocationData>();

        /// <summary>
        /// List of catalog locations to download in order (try remote first, then local)
        /// </summary>
        public List<ResourceLocationData> CatalogLocations
        {
            get { return m_CatalogLocations; }
        }

#if ENABLE_CCD
        /// <summary>
        /// Stores the CcdManager data to set the CCD properties to pull from.
        /// </summary>
        [SerializeField]
        CcdManagedData m_CcdManagedData;
        internal CcdManagedData CcdManagedData { get { return m_CcdManagedData; } set { m_CcdManagedData = value; } }
#endif
    }
}
