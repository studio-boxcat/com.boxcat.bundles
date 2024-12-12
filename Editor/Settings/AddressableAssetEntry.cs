using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets
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

        [SerializeField, HideInInspector]
        string m_Address;
        [ShowInInspector, DelayedProperty]
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
            SetDirty(AddressableCatalog.ModificationEvent.EntryModified, this, postEvent);
        }

        /// <summary>
        /// Is this entry for a scene.
        /// </summary>
        public bool IsScene => MainAssetType == typeof(SceneAsset);

        /// <summary>
        /// The System.Type of the main Object referenced by an AddressableAssetEntry
        /// </summary>
        public Type MainAssetType => MainAsset.GetType();

        internal AddressableAssetEntry(AssetGUID guid, AddressableAssetGroup parent)
        {
            m_GUID = guid.Value;
            m_ParentGroup = parent;
        }

        internal void InternalEvict()
        {
            Assert.IsNotNull(m_ParentGroup, "Entry is already evicted.");
            m_ParentGroup = null;
        }

        internal void SetDirty(AddressableCatalog.ModificationEvent e, object o, bool postEvent)
        {
            Assert.IsNotNull(m_ParentGroup, "Entry is already evicted.");
            m_ParentGroup.SetDirty(e, o, postEvent, true);
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
                if (m_MainAsset) return m_MainAsset;
                m_MainAsset = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(m_GUID));
                if (m_MainAsset is null) L.E("Failed to load asset: " + guid);
                return m_MainAsset;
            }

            set
            {
                m_MainAsset = value;
                SetDirty(AddressableCatalog.ModificationEvent.EntryModified, this, true);
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