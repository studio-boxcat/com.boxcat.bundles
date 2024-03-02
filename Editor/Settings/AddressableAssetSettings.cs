using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets.Settings
{
    /// <summary>
    /// Contains editor data for the addressables system.
    /// </summary>
    public class AddressableAssetSettings : ScriptableObject
    {
        internal class Cache<T1, T2>
        {
            private readonly AddressableAssetSettings m_Settings;
            private readonly Dictionary<T1, T2> m_Data = new();
            private int m_Version;

            public Cache(AddressableAssetSettings settings)
            {
                m_Settings = settings;
            }

            public bool TryGetCached(T1 key, out T2 result)
            {
                if (m_Version != m_Settings.version)
                {
                    result = default;
                    return false;
                }

                return m_Data.TryGetValue(key, out result);
            }

            public void Add(T1 key, T2 value)
            {
                if (m_Version != m_Settings.version)
                {
                    m_Data.Clear();
                    m_Version = m_Settings.version;
                }

                m_Data.Add(key, value);
            }
        }

        /// <summary>
        /// Options for labeling all the different generated events.
        /// </summary>
        public enum ModificationEvent
        {
            /// <summary>
            /// Use to indicate that a group was added to the settings object.
            /// </summary>
            GroupAdded,

            /// <summary>
            /// Use to indicate that a group was removed from the the settings object.
            /// </summary>
            GroupRemoved,

            /// <summary>
            /// Use to indicate that a group in the settings object was renamed.
            /// </summary>
            GroupRenamed,

            GroupModified,

            /// <summary>
            /// Use to indicate that an asset entry was created.
            /// </summary>
            EntryCreated,

            /// <summary>
            /// Use to indicate that an asset entry was added to a group.
            /// </summary>
            EntryAdded,

            /// <summary>
            /// Use to indicate that an asset entry moved from one group to another.
            /// </summary>
            EntryMoved,

            /// <summary>
            /// Use to indicate that an asset entry was removed from a group.
            /// </summary>
            EntryRemoved,

            /// <summary>
            /// Use to indicate that an asset entry was modified.
            /// </summary>
            EntryModified,

            /// <summary>
            /// Use to indicate that a batch of asset entries was modified. Note that the posted object will be null.
            /// </summary>
            BatchModification,
        }

        private string m_CachedAssetPath;

        /// <summary>
        /// The path of the settings asset.
        /// </summary>
        public string AssetPath
        {
            get
            {
                if (string.IsNullOrEmpty(m_CachedAssetPath) is false)
                    return m_CachedAssetPath;

                m_CachedAssetPath = AssetDatabase.GetAssetPath(this);
                Assert.IsFalse(string.IsNullOrEmpty(m_CachedAssetPath),
                    "AddressableAssetSettings must be an asset.  It must be saved to a location in the project.");
                return m_CachedAssetPath;
            }
        }

        private string m_CachedConfigFolder;

        /// <summary>
        /// The folder of the settings asset.
        /// </summary>
        public string ConfigFolder
        {
            get
            {
                if (string.IsNullOrEmpty(m_CachedConfigFolder))
                    m_CachedConfigFolder = Path.GetDirectoryName(AssetPath);
                return m_CachedConfigFolder;
            }
        }

        /// <summary>
        /// Event for handling settings changes.  The object passed depends on the event type.
        /// </summary>
        public Action<AddressableAssetSettings, ModificationEvent, object> OnModification { get; set; }

        /// <summary>
        /// Event for handling settings changes on all instances of AddressableAssetSettings.  The object passed depends on the event type.
        /// </summary>
        public static event Action<AddressableAssetSettings, ModificationEvent, object> OnModificationGlobal;

        [FormerlySerializedAs("m_defaultGroup")]
        [SerializeField]
        AddressableAssetGroup m_DefaultGroup;

        [SerializeField]
#if UNITY_2021_1_OR_NEWER
        bool m_NonRecursiveBuilding = true;
#else
        bool m_NonRecursiveBuilding = false;
#endif

        [SerializeField]
#if UNITY_2021_1_OR_NEWER
        bool m_ContiguousBundles = true;
#else
        bool m_ContiguousBundles = false;
#endif

        /// <summary>
        /// If set, packs assets in bundles contiguously based on the ordering of the source asset which results in improved asset loading times. Disable this if you've built bundles with a version of Addressables older than 1.12.1 and you want to minimize bundle changes.
        /// </summary>
        public bool ContiguousBundles => m_ContiguousBundles;

        /// <summary>
        /// If set, Calculates and build asset bundles using Non-Recursive Dependency calculation methods. This approach helps reduce asset bundle rebuilds and runtime memory consumption.
        /// </summary>
        public bool NonRecursiveBuilding => m_NonRecursiveBuilding;

        int m_Version;

        /// <summary>
        /// Hash of the current settings.  This value is recomputed if anything changes.
        /// </summary>
        public int version => m_Version;

        [SerializeField]
        List<AddressableAssetGroup> m_GroupAssets = new();

        /// <summary>
        /// List of asset groups.
        /// </summary>
        public List<AddressableAssetGroup> groups => m_GroupAssets;

        /// <summary>
        /// Remove an asset entry.
        /// </summary>
        /// <param name="guid">The  guid of the asset.</param>
        /// <param name="postEvent">Send modifcation event.</param>
        /// <returns>True if the entry was found and removed.</returns>
        public bool RemoveAssetEntry(AssetGUID guid, bool postEvent = true)
            => RemoveAssetEntry(FindAssetEntry(guid), postEvent);

        /// <summary>
        /// Remove an asset entry.
        /// </summary>
        /// <param name="entry">The entry to remove.</param>
        /// <param name="postEvent">Send modifcation event.</param>
        /// <returns>True if the entry was found and removed.</returns>
        internal bool RemoveAssetEntry(AddressableAssetEntry entry, bool postEvent = true)
        {
            if (entry == null)
                return false;
            if (entry.parentGroup != null)
                entry.parentGroup.RemoveAssetEntry(entry, postEvent);
            return true;
        }

        /// <summary>
        /// Create a new AddressableAssetSettings object.
        /// </summary>
        /// <param name="configFolder">The folder to store the settings object.</param>
        /// <param name="configName">The name of the settings object.</param>
        /// <returns></returns>
        public static AddressableAssetSettings Create(string configFolder, string configName)
        {
            var path = configFolder + "/" + configName + ".asset";
            var aa = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(path);
            if (aa != null) return aa;

            aa = CreateInstance<AddressableAssetSettings>();
            aa.name = configName;
            // TODO: Uncomment after initial opt-in testing period
            //aa.ContiguousBundles = true;

            Directory.CreateDirectory(configFolder);
            AssetDatabase.CreateAsset(aa, path);
            aa = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(path);
            AssetDatabase.SaveAssets();

            return aa;
        }

        /// <summary>
        /// Find asset group by functor.
        /// </summary>
        /// <param name="func">The functor to call on each group.  The first group that evaluates to true is returned.</param>
        /// <returns>The group found or null.</returns>
        public AddressableAssetGroup FindGroup(Func<AddressableAssetGroup, bool> func)
        {
            return groups.Find(g => g != null && func(g));
        }

        /// <summary>
        /// Find asset group by name.
        /// </summary>
        /// <param name="groupName">The name of the group.</param>
        /// <returns>The group found or null.</returns>
        public AddressableAssetGroup FindGroup(string groupName)
        {
            return FindGroup(g => g != null && g.Name == groupName);
        }

        /// <summary>
        /// The default group.  This group is used when marking assets as addressable via the inspector.
        /// </summary>
        public AddressableAssetGroup DefaultGroup
        {
            get
            {
                if (m_DefaultGroup == null)
                    m_DefaultGroup = m_GroupAssets[0];
                return m_DefaultGroup;
            }
            set
            {
                if (value == null)
                    L.E("Unable to set null as the Default Group.  Default Groups must not be ReadOnly.");
                else if (m_DefaultGroup != value)
                {
                    m_DefaultGroup = value;
                    SetDirty(ModificationEvent.BatchModification, null, false, true);
                }
            }
        }

        internal AddressableAssetEntry CreateEntry(AssetGUID guid, string address, AddressableAssetGroup parent, bool postEvent = true)
        {
            var entry = parent.GetAssetEntry(guid) ?? new AddressableAssetEntry(guid, address, parent);
            SetDirty(ModificationEvent.EntryCreated, entry, postEvent);
            return entry;
        }

        /// <summary>
        /// Marks the object as modified.
        /// </summary>
        /// <param name="modificationEvent">The event type that is changed.</param>
        /// <param name="eventData">The object data that corresponds to the event.</param>
        /// <param name="postEvent">If true, the event is propagated to callbacks.</param>
        /// <param name="settingsModified">If true, the settings asset will be marked as dirty.</param>
        public void SetDirty(ModificationEvent modificationEvent, object eventData, bool postEvent, bool settingsModified = false)
        {
            Assert.IsNotNull(this, "Object is destroyed.");

            if (postEvent)
            {
                OnModificationGlobal?.Invoke(this, modificationEvent, eventData);
                OnModification?.Invoke(this, modificationEvent, eventData);
            }

            if (settingsModified)
                EditorUtility.SetDirty(this);

            m_Version++;
        }

        private Cache<AssetGUID, AddressableAssetEntry> m_FindAssetEntryCache = null;

        /// <summary>
        /// Find and asset entry by guid.
        /// </summary>
        /// <param name="guid">The asset guid.</param>
        /// <returns>The found entry or null.</returns>
        public AddressableAssetEntry FindAssetEntry(AssetGUID guid)
        {
            if (m_FindAssetEntryCache != null)
            {
                if (m_FindAssetEntryCache.TryGetCached(guid, out var foundEntry))
                    return foundEntry;
            }
            else
                m_FindAssetEntryCache = new Cache<AssetGUID, AddressableAssetEntry>(this);

            foreach (var group in m_GroupAssets)
            {
                if (group == null) continue;
                var foundEntry = group.GetAssetEntry(guid);
                if (foundEntry == null) continue;
                m_FindAssetEntryCache.Add(guid, foundEntry);
                return foundEntry;
            }

            return null;
        }

        /// <summary>
        /// Move an existing entry to a group.
        /// </summary>
        /// <param name="entry">The entry to move.</param>
        /// <param name="targetParent">The group to add the entry to.</param>
        /// <param name="postEvent">Send modification event.</param>
        public static void MoveEntry(AddressableAssetEntry entry, AddressableAssetGroup targetParent, bool postEvent = true)
        {
            if (targetParent == null || entry == null)
                return;

            if (entry.parentGroup != null && entry.parentGroup != targetParent)
                entry.parentGroup.RemoveAssetEntry(entry, postEvent);

            targetParent.AddAssetEntry(entry, postEvent);
        }

        /// <summary>
        /// Create a new entries for each asset, or if one exists in a different group, move it into the targetParent group.
        /// </summary>
        /// <param name="guids">The asset guid's to move.</param>
        /// <param name="targetParent">The group to add the entries to.</param>
        /// <param name="createdEntries">List to add new entries to. If null, the list will be ignored.</param>
        /// <param name="movedEntries">List to add moved entries to. If null, the list will be ignored.</param>
        /// <param name="postEvent">Send modification event.</param>
        /// <exception cref="ArgumentException"></exception>
        internal void CreateOrMoveEntries(IEnumerable<AssetGUID> guids,
            AddressableAssetGroup targetParent,
            List<AddressableAssetEntry> createdEntries = null,
            List<AddressableAssetEntry> movedEntries = null,
            bool postEvent = true)
        {
            if (targetParent == null)
                throw new ArgumentException("targetParent must not be null");

            foreach (var guid in guids)
            {
                var entry = FindAssetEntry(guid);
                if (entry != null)
                {
                    MoveEntry(entry, targetParent, false);
                    movedEntries?.Add(entry);
                }
                else
                {
                    entry = CreateAndAddEntryToGroup(guid, targetParent, false);
                    if (entry != null && createdEntries != null)
                        createdEntries.Add(entry);
                }
            }

            if (postEvent)
                SetDirty(ModificationEvent.BatchModification, guids, true, true);
        }

        private AddressableAssetEntry CreateAndAddEntryToGroup(AssetGUID guid, AddressableAssetGroup targetParent, bool postEvent = true)
        {
            var entry = CreateEntry(guid, "", targetParent, postEvent);
            targetParent.AddAssetEntry(entry, postEvent);
            return entry;
        }

        /// <summary>
        /// Create a new asset group.
        /// </summary>
        /// <param name="postEvent">Post modification event.</param>
        /// <returns>The newly created group.</returns>
        public AddressableAssetGroup CreateGroup(bool postEvent)
        {
            // Prepare the AssetGroups folder & path to save the new group.
            var root = ConfigFolder + "/AssetGroups";
            if (!Directory.Exists(root)) Directory.CreateDirectory(root);
            var path = AssetDatabase.GenerateUniqueAssetPath(root + "/New Group.asset");
            var groupName = Path.GetFileNameWithoutExtension(path);

            // Create the new group and add it to the settings.
            var group = CreateInstance<AddressableAssetGroup>();
            AssetDatabase.CreateAsset(group, path);
            group.Initialize(this, groupName);
            groups.Add(group);

            // Mark the settings as dirty and post the event.
            SetDirty(ModificationEvent.GroupAdded, group, postEvent, true);
            return group;
        }

        internal bool IsNotUniqueGroupName(string groupName)
        {
            bool foundExisting = false;
            foreach (var g in groups)
            {
                if (g != null && g.Name == groupName)
                {
                    foundExisting = true;
                    break;
                }
            }

            return foundExisting;
        }

        internal void RemoveGroupInternal(AddressableAssetGroup g, bool deleteAsset, bool postEvent)
        {
            groups.Remove(g);
            SetDirty(ModificationEvent.GroupRemoved, g, postEvent, true);
            if (g == null || !deleteAsset) return;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(g, out var guidOfGroup, out long _) is false) return;

            var groupPath = AssetDatabase.GUIDToAssetPath(guidOfGroup);
            if (!string.IsNullOrEmpty(groupPath))
                AssetDatabase.DeleteAsset(groupPath);
        }
    }
}