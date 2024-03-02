using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets.Settings
{
    /// <summary>
    /// Contains data for an addressable asset entry.
    /// </summary>
    [Serializable]
    public class AddressableAssetEntry
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

        internal string m_cachedAssetPath = null;

        /// <summary>
        /// The path of the asset.
        /// </summary>
        public string AssetPath
        {
            get
            {
                if (string.IsNullOrEmpty(m_cachedAssetPath) is false)
                    return m_cachedAssetPath;
                m_cachedAssetPath = AssetDatabase.GUIDToAssetPath(m_GUID);
                return m_cachedAssetPath;
            }
        }

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
                Assert.IsNotNull(m_MainAsset, $"Failed to load asset at path {AssetPath} ({guid})");
                return m_MainAsset;
            }
        }

        /// <summary>
        /// Returns the address of the AddressableAssetEntry.
        /// </summary>
        /// <returns>The address of the AddressableAssetEntry</returns>
        public override string ToString() => m_Address;
    }
}