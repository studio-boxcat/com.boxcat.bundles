using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEngine;
using UnityEngine.AddressableAssets.Util;
using Assert = UnityEngine.Assertions.Assert;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Defines how bundles are created.
    /// </summary>
    enum BundlePackingMode
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
    public class AddressableAssetGroup : ScriptableObject
    {
        [SerializeField, ReadOnly, PropertyOrder(0)]
        AddressableCatalog m_Catalog;
        public AddressableCatalog Catalog => m_Catalog;

        [SerializeField, HideInInspector]
        BundlePackingMode m_BundleMode;
        [ShowInInspector, PropertyOrder(1)]
        internal BundlePackingMode BundleMode
        {
            get => m_BundleMode;
            set
            {
                if (m_BundleMode == value) return;
                m_BundleMode = value;
                SetDirty(AddressableCatalog.ModificationEvent.GroupModified, this, true, true);
            }
        }

        [SerializeField, LabelText("Entries"), PropertyOrder(2)]
        [TableList(AlwaysExpanded = true, ScrollViewHeight = 400)]
        [OnCollectionChanged(After = nameof(Entries_OnCollectionChanged_After))]
        List<AddressableAssetEntry> m_Entries = new();

        bool m_EntriesInitialized;

        public List<AddressableAssetEntry> entries
        {
            get
            {
                if (m_EntriesInitialized) return m_Entries;
                m_EntriesInitialized = true;
                foreach (var e in m_Entries)
                    e.parentGroup = this;
                return m_Entries;
            }
        }

        [CanBeNull]
        Dictionary<AssetGUID, AddressableAssetEntry> m_EntryMap;

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
                SetDirty(AddressableCatalog.ModificationEvent.GroupRenamed, this, true, true);
            }
        }

        internal Dictionary<AssetGUID, AddressableAssetEntry> EntryMap
        {
            get
            {
                if (m_EntryMap is not null)
                    return m_EntryMap;

                m_EntryMap = new Dictionary<AssetGUID, AddressableAssetEntry>();
                foreach (var e in entries)
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
                return m_EntryMap;
            }
        }

        /// <summary>
        /// Is the default group.
        /// </summary>
        public bool Default => ReferenceEquals(this, Catalog.DefaultGroup);

        internal void SetUp(AddressableCatalog catalog, string groupName)
        {
            m_Catalog = catalog;
            name = groupName;
        }

        /// <summary>
        /// Get an entry via the asset guid.
        /// </summary>
        /// <param name="guid">The asset guid.</param>
        /// <returns></returns>
        public AddressableAssetEntry GetAssetEntry(AssetGUID guid)
        {
            return EntryMap.GetValueOrDefault(guid);
        }

        internal void AddAssetEntry(AddressableAssetEntry e, bool postEvent = true)
        {
            Assert.AreNotEqual(typeof(DefaultAsset), e.MainAssetType, "Entry is a Folder.");
            e.parentGroup = this;
            m_Entries.Add(e);
            m_EntryMap?.Add(e.guid, e);
            SetDirty(AddressableCatalog.ModificationEvent.EntryAdded, e, postEvent, true);
        }

        /// <summary>
        /// Remove an entry.
        /// </summary>
        /// <param name="entry">The entry to remove.</param>
        /// <param name="postEvent">If true, post the event to callbacks.</param>
        public void RemoveAssetEntry(AddressableAssetEntry entry, bool postEvent = true)
        {
            entry.InternalEvict();
            m_Entries.Remove(entry);
            m_EntryMap?.Remove(entry.guid);
            SetDirty(AddressableCatalog.ModificationEvent.EntryRemoved, entry, postEvent, true);
        }

        /// <summary>
        /// Marks the object as modified.
        /// </summary>
        /// <param name="modificationEvent">The event type that is changed.</param>
        /// <param name="eventData">The object data that corresponds to the event.</param>
        /// <param name="postEvent">If true, the event is propagated to callbacks.</param>
        /// <param name="groupModified">If true, the group asset will be marked as dirty.</param>
        public void SetDirty(AddressableCatalog.ModificationEvent modificationEvent, object eventData, bool postEvent, bool groupModified = false)
        {
            Assert.IsNotNull(this, "AddressableAssetGroup is destroyed.");
            if (groupModified) EditorUtility.SetDirty(this);
            Catalog.SetDirty(modificationEvent, eventData, postEvent, false);
        }

        void Entries_OnCollectionChanged_After(CollectionChangeInfo info)
        {
            var entry = (AddressableAssetEntry) info.Value;
            AddressableCatalog.ModificationEvent e;

            switch (info.ChangeType)
            {
                case CollectionChangeType.Add or CollectionChangeType.Insert:
                    entry.parentGroup = this;
                    e = AddressableCatalog.ModificationEvent.EntryAdded;
                    break;
                case CollectionChangeType.RemoveIndex or CollectionChangeType.Clear:
                    entry.InternalEvict();
                    e = AddressableCatalog.ModificationEvent.EntryRemoved;
                    break;
                default:
                    throw new NotSupportedException($"Unhandled CollectionChangeType: {info.ChangeType}");
            }

            SetDirty(e, entry, true, true);
        }

        [Button, PropertyOrder(100)]
        internal void SortEntries()
        {
            m_Entries.Sort((x, y) =>
            {
                // any empty address should be at the end
                var emptyX = string.IsNullOrEmpty(x.address);
                var emptyY = string.IsNullOrEmpty(y.address);
                if (emptyX && !emptyY) return 1;
                if (emptyY && !emptyX) return -1;

                // Then sort by asset path
                var pathX = x.ResolveAssetPath();
                var pathY = y.ResolveAssetPath();
                return string.Compare(pathX, pathY, StringComparison.Ordinal);
            });

            SetDirty(AddressableCatalog.ModificationEvent.GroupModified, this, true, true);
        }
    }
}