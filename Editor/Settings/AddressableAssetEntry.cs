using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets.Settings
{
    /// <summary>
    /// Contains data for an addressable asset entry.
    /// </summary>
    [Serializable]
    public class AddressableAssetEntry : ISelfValidator
    {
        [SerializeField, HideInInspector]
        string m_GUID;
        public AssetGUID guid => (AssetGUID) m_GUID;

        [SerializeField, DisplayAsString]
        string m_Address;
        public string address
        {
            get => m_Address;
            set => SetAddress(value);
        }

        [NonSerialized]
        AddressableAssetGroup m_ParentGroup;

        /// <summary>
        /// The asset group that this entry belongs to.  An entry can only belong to a single group at a time.
        /// </summary>
        public AddressableAssetGroup parentGroup
        {
            get => m_ParentGroup;
            set => m_ParentGroup = value;
        }

        /// <summary>
        /// Set the address of the entry.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <param name="postEvent">Post modification event.</param>
        public void SetAddress(string addr, bool postEvent = true)
        {
            if (m_Address == addr) return;
            m_Address = addr;
            SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, this, postEvent);
        }

        /// <summary>
        /// Is this entry for a scene.
        /// </summary>
        public bool IsScene => MainAssetType == typeof(SceneAsset);

        /// <summary>
        /// The System.Type of the main Object referenced by an AddressableAssetEntry
        /// </summary>
        public Type MainAssetType => MainAsset.GetType();

        internal AddressableAssetEntry(AssetGUID guid, string address, AddressableAssetGroup parent)
        {
            m_GUID = guid.Value;
            m_Address = address;
            parentGroup = parent;
        }

        internal void SetDirty(AddressableAssetSettings.ModificationEvent e, object o, bool postEvent)
        {
            if (parentGroup != null)
                parentGroup.SetDirty(e, o, postEvent, true);
        }

        [NonSerialized]
        string m_cachedAssetPath;

        /// <summary>
        /// The path of the asset.
        /// </summary>
        public string AssetPath
        {
            get
            {
                if (m_cachedAssetPath is not null)
                    return m_cachedAssetPath;
                AddressableAssetEntryTracker.Track(this);
                Assert.IsNotNull(m_cachedAssetPath, "Failed to track asset path: " + guid);
                return m_cachedAssetPath;
            }
        }

        internal void ResetAssetPath(string path) => m_cachedAssetPath = path;

        private Object m_MainAsset;

        /// <summary>
        /// The main asset object for this entry.
        /// </summary>
        [ShowInInspector]
        public Object MainAsset
        {
            get
            {
                if (m_MainAsset != null)
                    return m_MainAsset;
                m_MainAsset = AssetDatabase.LoadAssetAtPath<Object>(AssetPath);
                if (m_MainAsset is null)
                    L.E("Failed to load asset: " + guid);
                return m_MainAsset;
            }
        }

        /// <summary>
        /// Returns the address of the AddressableAssetEntry.
        /// </summary>
        /// <returns>The address of the AddressableAssetEntry</returns>
        public override string ToString() => m_Address;

        void ISelfValidator.Validate(SelfValidationResult result)
        {
            if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(m_GUID)))
                result.AddError("Asset not found for entry: " + guid);
        }
    }
}