using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.AddressableAssets;
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
            private AddressableAssetSettings m_Settings;
            private Hash128 m_CurrentCacheVersion;
            private Dictionary<T1, T2> m_TargetInfoCache = new();

            public Cache(AddressableAssetSettings settings)
            {
                m_Settings = settings;
            }

            public bool TryGetCached(T1 key, out T2 result)
            {
                if (IsValid() && m_TargetInfoCache.TryGetValue(key, out result))
                    return true;

                result = default;
                return false;
            }

            public void Add(T1 key, T2 value)
            {
                if (!IsValid())
                    m_CurrentCacheVersion = m_Settings.currentHash;
                m_TargetInfoCache.Add(key, value);
            }

            private bool IsValid()
            {
                if (m_TargetInfoCache.Count == 0)
                    return false;
                if (m_CurrentCacheVersion.isValid && m_CurrentCacheVersion.Equals(m_Settings.currentHash))
                    return true;

                m_TargetInfoCache.Clear();
                m_CurrentCacheVersion = default;
                return false;
            }
        }

        private Cache<AssetGUID, AddressableAssetEntry> m_FindAssetEntryCache = null;

        /// <summary>
        /// Default name of a newly created group.
        /// </summary>
        public const string kNewGroupName = "New Group";

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
            /// Use to indicate that a new build script is being used as the active build script.
            /// </summary>
            ActiveBuildScriptChanged,

            /// <summary>
            /// Use to indicate that a new data builder script was added to the settings object.
            /// </summary>
            DataBuilderAdded,

            /// <summary>
            /// Use to indicate that a data builder script was removed from the settings object.
            /// </summary>
            DataBuilderRemoved,

            /// <summary>
            /// Use to indicate that a new script is being used as the active playmode data builder.
            /// </summary>
            ActivePlayModeScriptChanged,

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
        /// The folder for the group assets.
        /// </summary>
        public string GroupFolder => ConfigFolder + "/AssetGroups";

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

        [FormerlySerializedAs("m_cachedHash")]
        [SerializeField, HideInInspector]
        Hash128 m_currentHash;

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

        Hash128 m_GroupsHash;
        Hash128 groupsHash
        {
            get
            {
                if (m_GroupsHash.isValid)
                    return m_GroupsHash;

                var count = 0;
                foreach (var g in m_GroupAssets)
                {
                    // this ignores both null values and deleted managed objects
                    if (g == null) continue;
                    count += 1;
                    var gah = g.currentHash;
                    m_GroupsHash.Append(ref gah);
                }
                m_GroupsHash.Append(ref count);
                return m_GroupsHash;
            }
        }

        /// <summary>
        /// Hash of the current settings.  This value is recomputed if anything changes.
        /// </summary>
        public Hash128 currentHash
        {
            get
            {
                if (m_currentHash.isValid) return m_currentHash;
                var subHashes = new[] {groupsHash};
                m_currentHash.Append(subHashes);
                return m_currentHash;
            }
        }

        [SerializeField]
        List<AddressableAssetGroup> m_GroupAssets = new();

        /// <summary>
        /// List of asset groups.
        /// </summary>
        public List<AddressableAssetGroup> groups => m_GroupAssets;

        [SerializeField]
        byte m_ActivePlayerDataBuilderIndex = 3;

        [SerializeField]
        List<ScriptableObject> m_DataBuilders = new();

        /// <summary>
        /// List of ScriptableObjects that implement the IDataBuilder interface.  These are used to create data for editor play mode and for player builds.
        /// </summary>
        public List<ScriptableObject> DataBuilders => m_DataBuilders;

        /// <summary>
        /// Get The data builder at a specifc index.
        /// </summary>
        /// <param name="index">The index of the builder.</param>
        /// <returns>The data builder at the specified index.</returns>
        public IDataBuilder GetDataBuilder(byte index)
        {
            if (m_DataBuilders.Count == 0)
                return null;

            if (index >= m_DataBuilders.Count)
            {
                Debug.LogWarningFormat("Invalid index for data builder: {0}.", index);
                return null;
            }

            return m_DataBuilders[index] as IDataBuilder;
        }

        /// <summary>
        /// Get the active data builder for player data.
        /// </summary>
        public IDataBuilder ActivePlayerDataBuilder => GetDataBuilder(m_ActivePlayerDataBuilderIndex);

        /// <summary>
        /// Get the active data builder for editor play mode data.
        /// </summary>
        public IDataBuilder ActivePlayModeDataBuilder => GetDataBuilder(ProjectConfigData.ActivePlayModeIndex);

        /// <summary>
        /// Get the index of the active player data builder.
        /// </summary>
        public byte ActivePlayerDataBuilderIndex
        {
            get => m_ActivePlayerDataBuilderIndex;
            set
            {
                if (m_ActivePlayerDataBuilderIndex != value)
                {
                    m_ActivePlayerDataBuilderIndex = value;
                    SetDirty(ModificationEvent.ActiveBuildScriptChanged, ActivePlayerDataBuilder, true, true);
                }
            }
        }

        /// <summary>
        /// Get the index of the active play mode data builder.
        /// </summary>
        public byte ActivePlayModeDataBuilderIndex
        {
            get => ProjectConfigData.ActivePlayModeIndex;
            set
            {
                ProjectConfigData.ActivePlayModeIndex = value;
                SetDirty(ModificationEvent.ActivePlayModeScriptChanged, ActivePlayModeDataBuilder, true, false);
            }
        }

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
            if (this != null)
            {
                if (postEvent)
                {
                    if (OnModificationGlobal != null)
                        OnModificationGlobal(this, modificationEvent, eventData);
                    if (OnModification != null)
                        OnModification(this, modificationEvent, eventData);
                }

                if (settingsModified && true)
                    EditorUtility.SetDirty(this);
            }
            if (EventAffectsGroups(modificationEvent))
                m_GroupsHash = default;
            m_currentHash = default;
        }

        private bool EventAffectsGroups(ModificationEvent modificationEvent)
        {
            switch (modificationEvent)
            {
                case ModificationEvent.BatchModification:
                case ModificationEvent.EntryAdded:
                case ModificationEvent.EntryCreated:
                case ModificationEvent.EntryModified:
                case ModificationEvent.EntryMoved:
                case ModificationEvent.EntryRemoved:
                case ModificationEvent.GroupAdded:
                case ModificationEvent.GroupRemoved:
                case ModificationEvent.GroupRenamed:
                case ModificationEvent.GroupModified:
                    return true;
            }
            return false;
        }

        internal bool RemoveMissingGroupReferences()
        {
            List<int> missingGroupsIndices = new List<int>();
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g == null)
                    missingGroupsIndices.Add(i);
            }

            if (missingGroupsIndices.Count > 0)
            {
                Debug.Log("Addressable settings contains " + missingGroupsIndices.Count + " group reference(s) that are no longer there. Removing reference(s).");
                for (int i = missingGroupsIndices.Count - 1; i >= 0; i--)
                    groups.RemoveAt(missingGroupsIndices[i]);

                return true;
            }

            return false;
        }

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
                if (group == null)
                    continue;

                var foundEntry = group.GetAssetEntry(guid);
                if (foundEntry != null)
                {
                    m_FindAssetEntryCache.Add(guid, foundEntry);
                    return foundEntry;
                }
            }

            return null;
        }

        /// <summary>
        /// Move an existing entry to a group.
        /// </summary>
        /// <param name="entry">The entry to move.</param>
        /// <param name="targetParent">The group to add the entry to.</param>
        /// <param name="postEvent">Send modification event.</param>
        public void MoveEntry(AddressableAssetEntry entry, AddressableAssetGroup targetParent, bool postEvent = true)
        {
            if (targetParent == null || entry == null)
                return;

            if (entry.parentGroup != null && entry.parentGroup != targetParent)
                entry.parentGroup.RemoveAssetEntry(entry, postEvent);

            targetParent.AddAssetEntry(entry, postEvent);
        }

        /// <summary>
        /// Create a new entry, or if one exists in a different group, move it into the new group.
        /// </summary>
        /// <param name="guid">The asset guid.</param>
        /// <param name="targetParent">The group to add the entry to.</param>
        /// <param name="postEvent">Send modification event.</param>
        /// <returns></returns>
        public AddressableAssetEntry CreateOrMoveEntry(AssetGUID guid, AddressableAssetGroup targetParent, bool postEvent = true)
        {
            if (targetParent == null || guid.IsInvalid())
                return null;

            var entry = FindAssetEntry(guid);
            if (entry != null) //move entry to where it should go...
            {
                //no need to do anything if already done...
                if (entry.parentGroup == targetParent && !postEvent)
                    return entry;

                MoveEntry(entry, targetParent, postEvent);
            }
            else //create entry
            {
                entry = CreateAndAddEntryToGroup(guid, targetParent, postEvent);
            }

            return entry;
        }

        /// <summary>
        /// Create a new entries for each asset, or if one exists in a different group, move it into the targetParent group.
        /// </summary>
        /// <param name="guids">The asset guid's to move.</param>
        /// <param name="targetParent">The group to add the entries to.</param>
        /// <param name="createdEntries">List to add new entries to. If null, the list will be ignored.</param>
        /// <param name="movedEntries">List to add moved entries to. If null, the list will be ignored.</param>
        /// <param name="readOnly">Is the new entry read only.</param>
        /// <param name="postEvent">Send modification event.</param>
        /// <exception cref="ArgumentException"></exception>
        internal void CreateOrMoveEntries(IEnumerable<AssetGUID> guids,
            AddressableAssetGroup targetParent,
            List<AddressableAssetEntry> createdEntries = null,
            List<AddressableAssetEntry> movedEntries = null,
            bool readOnly = false,
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
            AddressableAssetEntry entry = null;
            var path = AssetDatabase.GUIDToAssetPath(guid.Value);

            if (AddressableAssetUtility.IsPathValidForEntry(path))
            {
                entry = CreateEntry(guid, path, targetParent, postEvent);
            }
            else
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(path) != null && BuildUtility.IsEditorType(AssetDatabase.GetMainAssetTypeAtPath(path)))
                    return null;
                entry = CreateEntry(guid, guid.Value, targetParent, postEvent);
            }

            targetParent.AddAssetEntry(entry, postEvent);
            return entry;
        }

        /// <summary>
        /// Create a new asset group.
        /// </summary>
        /// <param name="groupName">The group name.</param>
        /// <param name="setAsDefaultGroup">Set the new group as the default group.</param>
        /// <param name="postEvent">Post modification event.</param>
        /// <returns>The newly created group.</returns>
        public AddressableAssetGroup CreateGroup(string groupName, bool setAsDefaultGroup, bool postEvent)
        {
            if (string.IsNullOrEmpty(groupName))
                groupName = kNewGroupName;
            var validName = FindUniqueGroupName(groupName);

            var group = CreateInstance<AddressableAssetGroup>();
            group.Initialize(this, validName, GUID.Generate().ToString());

            if (true)
            {
                if (!Directory.Exists(GroupFolder))
                    Directory.CreateDirectory(GroupFolder);
                AssetDatabase.CreateAsset(group, GroupFolder + "/" + group.Name + ".asset");
            }

            if (!m_GroupAssets.Contains(group))
                groups.Add(group);

            if (setAsDefaultGroup)
                DefaultGroup = group;
            SetDirty(ModificationEvent.GroupAdded, group, postEvent, true);
            return group;
        }

        internal string FindUniqueGroupName(string potentialName)
        {
            var cleanedName = potentialName.Replace('/', '-');
            cleanedName = cleanedName.Replace('\\', '-');
            if (cleanedName != potentialName)
                L.I("Group names cannot include '\\' or '/'.  Replacing with '-'. " + cleanedName);
            var validName = cleanedName;
            int index = 1;
            bool foundExisting = true;
            while (foundExisting)
            {
                if (index > 1000)
                {
                    L.E("Unable to create valid name for new Addressable Assets group.");
                    return cleanedName;
                }

                foundExisting = IsNotUniqueGroupName(validName);
                if (foundExisting)
                {
                    validName = cleanedName + index;
                    index++;
                }
            }

            return validName;
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

        /// <summary>
        /// Remove an asset group.
        /// </summary>
        /// <param name="g"></param>
        public void RemoveGroup(AddressableAssetGroup g)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                RemoveGroupInternal(g, true, true);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
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

        /// <summary>
        /// Runs the active player data build script to create runtime data.
        /// See the [BuildPlayerContent](xref:addressables-api-build-player-content) documentation for more details.
        /// </summary>
        public static void BuildPlayerContent()
        {
            BuildPlayerContent(out _);
        }

        /// <summary>
        /// Runs the active player data build script to create runtime data.
        /// See the [BuildPlayerContent](xref:addressables-api-build-player-content) documentation for more details.
        /// </summary>
        /// <param name="result">Results from running the active player data build script.</param>
        public static void BuildPlayerContent(out AddressablesPlayerBuildResult result)
        {
            BuildPlayerContent(out result, null);
        }

        internal static void BuildPlayerContent(out AddressablesPlayerBuildResult result, AddressablesDataBuilderInput input)
        {
            var settings = input != null ? input.AddressableSettings : AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                string error;
                if (EditorApplication.isUpdating)
                    error = "Addressable Asset Settings does not exist.  EditorApplication.isUpdating was true.";
                else if (EditorApplication.isCompiling)
                    error = "Addressable Asset Settings does not exist.  EditorApplication.isCompiling was true.";
                else
                    error = "Addressable Asset Settings does not exist.  Failed to create.";
                Debug.LogError(error);
                result = new AddressablesPlayerBuildResult();
                result.Error = error;
                return;
            }

            result = settings.BuildPlayerContentImpl(input);
        }

        internal AddressablesPlayerBuildResult BuildPlayerContentImpl(AddressablesDataBuilderInput buildContext = null, bool buildAndRelease = false)
        {
            if (Directory.Exists(PathConfig.BuildPath))
            {
                try
                {
                    Directory.Delete(PathConfig.BuildPath, true);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            if (buildContext == null)
                buildContext = new AddressablesDataBuilderInput(this);

            buildContext.IsBuildAndRelease = buildAndRelease;
            var result = ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(buildContext);
            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError(result.Error);
                Debug.LogError($"Addressable content build failure (duration : {TimeSpan.FromSeconds(result.Duration).ToString("g")})");
            }
            else
                Debug.Log($"Addressable content successfully built (duration : {TimeSpan.FromSeconds(result.Duration).ToString("g")})");

            AssetDatabase.Refresh();
            return result;
        }

        /// <summary>
        /// Deletes all created runtime data for the active player data builder.
        /// </summary>
        /// <param name="builder">The builder to call ClearCachedData on.  If null, all builders will be cleaned</param>
        public static void CleanPlayerContent(IDataBuilder builder = null)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                if (EditorApplication.isUpdating)
                    Debug.LogError("Addressable Asset Settings does not exist.  EditorApplication.isUpdating was true.");
                else if (EditorApplication.isCompiling)
                    Debug.LogError("Addressable Asset Settings does not exist.  EditorApplication.isCompiling was true.");
                else
                    Debug.LogError("Addressable Asset Settings does not exist.  Failed to create.");
                return;
            }

            settings.CleanPlayerContentImpl(builder);
        }

        internal void CleanPlayerContentImpl(IDataBuilder builder = null)
        {
            if (builder != null)
            {
                builder.ClearCachedData();
            }
            else
            {
                for (byte i = 0; i < DataBuilders.Count; i++)
                {
                    var m = GetDataBuilder(i);
                    m.ClearCachedData();
                }
            }

            AssetDatabase.Refresh();
        }

        static Dictionary<string, Action<IEnumerable<AddressableAssetEntry>>> s_CustomAssetEntryCommands = new Dictionary<string, Action<IEnumerable<AddressableAssetEntry>>>();

        /// <summary>
        /// Register a custom command to process asset entries.  These commands will be shown in the context menu of the groups window.
        /// </summary>
        /// <param name="cmdId">The id of the command.  This will be used for the display name of the context menu item.</param>
        /// <param name="cmdFunc">The command handler function.</param>
        /// <returns>Returns true if the command was registered.</returns>
        public static bool RegisterCustomAssetEntryCommand(string cmdId, Action<IEnumerable<AddressableAssetEntry>> cmdFunc)
        {
            if (string.IsNullOrEmpty(cmdId))
            {
                Debug.LogError("RegisterCustomAssetEntryCommand - invalid command id.");
                return false;
            }

            if (cmdFunc == null)
            {
                Debug.LogError($"RegisterCustomAssetEntryCommand - command functor for id '{cmdId}'.");
                return false;
            }

            s_CustomAssetEntryCommands[cmdId] = cmdFunc;
            return true;
        }

        /// <summary>
        /// Removes a registered custom entry command.
        /// </summary>
        /// <param name="cmdId">The command id.</param>
        /// <returns>Returns true if the command was removed.</returns>
        public static bool UnregisterCustomAssetEntryCommand(string cmdId)
        {
            if (string.IsNullOrEmpty(cmdId))
            {
                Debug.LogError("UnregisterCustomAssetEntryCommand - invalid command id.");
                return false;
            }

            if (!s_CustomAssetEntryCommands.Remove(cmdId))
            {
                Debug.LogError($"UnregisterCustomAssetEntryCommand - command id '{cmdId}' is not registered.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Invoke a registered command for a set of entries.
        /// </summary>
        /// <param name="cmdId">The id of the command.</param>
        /// <param name="entries">The entries to run the command on.</param>
        /// <returns>Returns true if the command was executed without exceptions.</returns>
        public static bool InvokeAssetEntryCommand(string cmdId, IEnumerable<AddressableAssetEntry> entries)
        {
            try
            {
                if (string.IsNullOrEmpty(cmdId) || !s_CustomAssetEntryCommands.ContainsKey(cmdId))
                {
                    Debug.LogError($"Asset Entry Command '{cmdId}' not found.  Ensure that it is registered by calling RegisterCustomAssetEntryCommand.");
                    return false;
                }

                if (entries == null)
                {
                    Debug.LogError($"Asset Entry Command '{cmdId}' called with null entry collection.");
                    return false;
                }

                s_CustomAssetEntryCommands[cmdId](entries);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Encountered exception when running Asset Entry Command '{cmdId}': {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// The ids of the registered commands.
        /// </summary>
        public static IEnumerable<string> CustomAssetEntryCommands => s_CustomAssetEntryCommands.Keys;

        static Dictionary<string, Action<IEnumerable<AddressableAssetGroup>>> s_CustomAssetGroupCommands = new Dictionary<string, Action<IEnumerable<AddressableAssetGroup>>>();

        /// <summary>
        /// Register a custom command to process asset groups.  These commands will be shown in the context menu of the groups window.
        /// </summary>
        /// <param name="cmdId">The id of the command.  This will be used for the display name of the context menu item.</param>
        /// <param name="cmdFunc">The command handler function.</param>
        /// <returns>Returns true if the command was registered.</returns>
        public static bool RegisterCustomAssetGroupCommand(string cmdId, Action<IEnumerable<AddressableAssetGroup>> cmdFunc)
        {
            if (string.IsNullOrEmpty(cmdId))
            {
                Debug.LogError("RegisterCustomAssetGroupCommand - invalid command id.");
                return false;
            }

            if (cmdFunc == null)
            {
                Debug.LogError($"RegisterCustomAssetGroupCommand - command functor for id '{cmdId}'.");
                return false;
            }

            s_CustomAssetGroupCommands[cmdId] = cmdFunc;
            return true;
        }

        /// <summary>
        /// Removes a registered custom group command.
        /// </summary>
        /// <param name="cmdId">The command id.</param>
        /// <returns>Returns true if the command was removed.</returns>
        public static bool UnregisterCustomAssetGroupCommand(string cmdId)
        {
            if (string.IsNullOrEmpty(cmdId))
            {
                Debug.LogError("UnregisterCustomAssetGroupCommand - invalid command id.");
                return false;
            }

            if (!s_CustomAssetGroupCommands.Remove(cmdId))
            {
                Debug.LogError($"UnregisterCustomAssetGroupCommand - command id '{cmdId}' is not registered.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Invoke a registered command for a set of groups.
        /// </summary>
        /// <param name="cmdId">The id of the command.</param>
        /// <param name="groups">The groups to run the command on.</param>
        /// <returns>Returns true if the command was invoked successfully.</returns>
        public static bool InvokeAssetGroupCommand(string cmdId, IEnumerable<AddressableAssetGroup> groups)
        {
            try
            {
                if (string.IsNullOrEmpty(cmdId) || !s_CustomAssetGroupCommands.ContainsKey(cmdId))
                {
                    Debug.LogError($"Asset Group Command '{cmdId}' not found.  Ensure that it is registered by calling RegisterCustomAssetGroupCommand.");
                    return false;
                }

                if (groups == null)
                {
                    Debug.LogError($"Asset Group Command '{cmdId}' called with null group collection.");
                    return false;
                }

                s_CustomAssetGroupCommands[cmdId](groups);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Encountered exception when running Asset Group Command '{cmdId}': {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// The ids of the registered commands.
        /// </summary>
        public static IEnumerable<string> CustomAssetGroupCommands => s_CustomAssetGroupCommands.Keys;
    }
}