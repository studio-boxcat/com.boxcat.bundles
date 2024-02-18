using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.Settings
{
    /// <summary>
    /// Defines how bundles are created.
    /// </summary>
    public enum BundlePackingMode
    {
        /// <summary>
        /// Creates a bundle for all non-scene entries and another for all scenes entries.
        /// </summary>
        PackTogether,

        /// <summary>
        /// Creates a bundle per entry.  This is useful if each entry is a folder as all sub entries will go to the same bundle.
        /// </summary>
        PackSeparately,
    }

    /// <summary>
    /// Contains the collection of asset entries associated with this group.
    /// </summary>
    [Serializable]
    public class AddressableAssetGroup : ScriptableObject, IComparer<AddressableAssetEntry>, ISerializationCallbackReceiver
    {
        [SerializeField, ReadOnly, PropertyOrder(0)]
        AddressableAssetSettings m_Settings;

        [SerializeField, HideInInspector]
        BundlePackingMode m_BundleMode;
        [ShowInInspector, PropertyOrder(1)]
        public BundlePackingMode BundleMode
        {
            get => m_BundleMode;
            set
            {
                if (m_BundleMode == value) return;
                m_BundleMode = value;
                SetDirty(AddressableAssetSettings.ModificationEvent.GroupModified, this, true, true);
            }
        }

        [SerializeField, LabelText("Entries"), TableList(IsReadOnly = true), PropertyOrder(2)]
        List<AddressableAssetEntry> m_SerializeEntries = new();

        Dictionary<AssetGUID, AddressableAssetEntry> m_EntryMap = new();

        /// <summary>
        /// The group name.
        /// </summary>
        public string Name
        {
            get => name;
            set
            {
                if (name == value)
                    return;

                // value should be valid for a file name.
                Assert.IsFalse(name.Contains('/'), "AddressableAssetGroup.Name cannot contain a '/' character.");
                Assert.IsFalse(name.Contains('\\'), "AddressableAssetGroup.Name cannot contain a '\\' character.");
                Assert.IsTrue(AssetDatabase.Contains(this), "AddressableAssetGroup.Name can only be set on assets that are persisted.");

                name = value;
                SetDirty(AddressableAssetSettings.ModificationEvent.GroupRenamed, this, true, true);
            }
        }

        string m_GUID;

        /// <summary>
        /// The group GUID.
        /// </summary>
        public string Guid
        {
            get
            {
                if (!string.IsNullOrEmpty(m_GUID)) return m_GUID;
                var isAsset = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(this, out m_GUID, out long _);
                Assert.IsTrue(isAsset, "AddressableAssetGroup is not an asset.");
                return m_GUID;
            }
        }

        /// <summary>
        /// The AddressableAssetSettings that this group belongs to.
        /// </summary>
        public AddressableAssetSettings Settings
        {
            get
            {
                if (m_Settings == null)
                    m_Settings = AddressableAssetSettingsDefaultObject.Settings;

                return m_Settings;
            }
        }

        /// <summary>
        /// The collection of asset entries.
        /// </summary>
        public virtual ICollection<AddressableAssetEntry> entries => m_EntryMap.Values;

        internal Dictionary<AssetGUID, AddressableAssetEntry> EntryMap => m_EntryMap;

        /// <summary>
        /// Is the default group.
        /// </summary>
        public bool Default => Guid == Settings.DefaultGroup.Guid;

        /// <summary>
        /// Compares two asset entries based on their guids.
        /// </summary>
        /// <param name="x">The first entry to compare.</param>
        /// <param name="y">The second entry to compare.</param>
        /// <returns>Returns 0 if both entries are null or equivalent.
        /// Returns -1 if the first entry is null or the first entry precedes the second entry in the sort order.
        /// Returns 1 if the second entry is null or the first entry follows the second entry in the sort order.</returns>
        public int Compare(AddressableAssetEntry x, AddressableAssetEntry y)
        {
            return x switch
            {
                null when y is null => 0,
                null => -1,
                _ => y is null ? 1 : x.guid.CompareTo(y.guid)
            };
        }

        Hash128 m_CurrentHash;
        internal Hash128 currentHash
        {
            get
            {
                if (!m_CurrentHash.isValid)
                {
                    m_CurrentHash.Append(name);
                    m_CurrentHash.Append(m_GUID.GetHashCode());
                    m_CurrentHash.Append(entries.Count);
                    foreach (var e in entries)
                        m_CurrentHash.Append(e.guid.Value);
                }
                return m_CurrentHash;
            }
        }

        /// <summary>
        /// Converts data to serializable format.
        /// </summary>
        public void OnBeforeSerialize()
        {
            if (m_SerializeEntries == null)
            {
                m_SerializeEntries = new List<AddressableAssetEntry>(entries.Count);
                foreach (var e in entries)
                    m_SerializeEntries.Add(e);
            }
        }

        /// <summary>
        /// Converts data from serializable format.
        /// </summary>
        public void OnAfterDeserialize()
        {
            ResetEntryMap();
        }

        internal void ResetEntryMap()
        {
            m_EntryMap.Clear();
            foreach (var e in m_SerializeEntries)
            {
                try
                {
                    e.parentGroup = this;
                    m_EntryMap.Add(e.guid, e);
                }
                catch (Exception ex)
                {
                    L.I(e.address);
                    Debug.LogException(ex);
                }
            }
        }

        internal void Initialize(AddressableAssetSettings settings, string groupName, string guid)
        {
            m_Settings = settings;
            name = groupName;
            m_GUID = guid;
        }

        internal void AddAssetEntry(AddressableAssetEntry e, bool postEvent = true)
        {
            e.parentGroup = this;
            m_EntryMap[e.guid] = e;
            if (!string.IsNullOrEmpty(e.AssetPath) && e.MainAssetType == typeof(DefaultAsset) && AssetDatabase.IsValidFolder(e.AssetPath))
                throw new NotSupportedException(e.AssetPath);
            m_SerializeEntries = null;
            SetDirty(AddressableAssetSettings.ModificationEvent.EntryAdded, e, postEvent, true);
        }

        /// <summary>
        /// Get an entry via the asset guid.
        /// </summary>
        /// <param name="guid">The asset guid.</param>
        /// <returns></returns>
        public virtual AddressableAssetEntry GetAssetEntry(AssetGUID guid)
        {
            return m_EntryMap.GetValueOrDefault(guid);
        }

        /// <summary>
        /// Marks the object as modified.
        /// </summary>
        /// <param name="modificationEvent">The event type that is changed.</param>
        /// <param name="eventData">The object data that corresponds to the event.</param>
        /// <param name="postEvent">If true, the event is propagated to callbacks.</param>
        /// <param name="groupModified">If true, the group asset will be marked as dirty.</param>
        public void SetDirty(AddressableAssetSettings.ModificationEvent modificationEvent, object eventData, bool postEvent, bool groupModified = false)
        {
            m_CurrentHash = default;
            if (Settings == null) return;
            if (groupModified && this != null)
                EditorUtility.SetDirty(this);
            Settings.SetDirty(modificationEvent, eventData, postEvent, false);
        }

        /// <summary>
        /// Remove an entry.
        /// </summary>
        /// <param name="entry">The entry to remove.</param>
        /// <param name="postEvent">If true, post the event to callbacks.</param>
        public void RemoveAssetEntry(AddressableAssetEntry entry, bool postEvent = true)
        {
            m_EntryMap.Remove(entry.guid);
            entry.parentGroup = null;
            m_SerializeEntries = null;
            SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, entry, postEvent, true);
        }

        internal void RemoveAssetEntries(IEnumerable<AddressableAssetEntry> removeEntries, bool postEvent = true)
        {
            foreach (var entry in removeEntries)
            {
                m_EntryMap.Remove(entry.guid);
                entry.parentGroup = null;
            }

            if (removeEntries.Any())
            {
                m_SerializeEntries = null;
                SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, removeEntries.ToArray(), postEvent, true);
            }
        }
    }
}