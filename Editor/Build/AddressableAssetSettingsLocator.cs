using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Assertions;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using static UnityEditor.AddressableAssets.Settings.AddressablesFileEnumeration;

namespace UnityEditor.AddressableAssets.Settings
{
    internal class AddressableAssetSettingsLocator : IResourceLocator
    {
        public Dictionary<string, AddressableAssetEntry> m_keyToEntry;
        public Dictionary<CacheKey, IResourceLocation> m_Cache;
        public AddressableAssetTree m_AddressableAssetTree;
        AddressableAssetSettings m_Settings;
        bool m_dirty = true;

        public struct CacheKey : IEquatable<CacheKey>
        {
            public object m_key;
            public Type m_type;

            public bool Equals(CacheKey other)
            {
                if (!m_key.Equals(other.m_key))
                    return false;
                return m_type == other.m_type;
            }

            public override int GetHashCode() => m_key.GetHashCode() * 31 + (m_type == null ? 0 : m_type.GetHashCode());
        }

        public AddressableAssetSettingsLocator(AddressableAssetSettings settings)
        {
            m_Settings = settings;
            m_dirty = true;
            m_Settings.OnModification += Settings_OnModification;
        }

        void RebuildInternalData()
        {
            m_AddressableAssetTree = BuildAddressableTree(m_Settings);
            m_Cache = new Dictionary<CacheKey, IResourceLocation>();
            m_keyToEntry = new Dictionary<string, AddressableAssetEntry>();
            using (new AddressablesFileEnumerationScope(m_AddressableAssetTree))
            {
                foreach (AddressableAssetGroup g in m_Settings.groups)
                {
                    if (g == null)
                        continue;

                    foreach (AddressableAssetEntry e in g.entries)
                        AddEntriesToTables(m_keyToEntry, e);
                }
            }

            m_dirty = false;
        }

        private void Settings_OnModification(AddressableAssetSettings settings, AddressableAssetSettings.ModificationEvent evt, object arg3)
        {
            switch (evt)
            {
                case AddressableAssetSettings.ModificationEvent.EntryAdded:
                case AddressableAssetSettings.ModificationEvent.EntryCreated:
                case AddressableAssetSettings.ModificationEvent.EntryModified:
                case AddressableAssetSettings.ModificationEvent.EntryMoved:
                case AddressableAssetSettings.ModificationEvent.EntryRemoved:
                case AddressableAssetSettings.ModificationEvent.GroupRemoved:
                case AddressableAssetSettings.ModificationEvent.BatchModification:
                    m_dirty = true;
                    break;
            }
        }

        static void AddEntriesToTables(Dictionary<string, AddressableAssetEntry> keyToEntries, AddressableAssetEntry e)
        {
            keyToEntries.Add(e.address, e);
            keyToEntries.Add(e.guid, e);
        }

        static void GatherEntryLocations(AddressableAssetEntry entry, [NotNull] Type type, IList<IResourceLocation> locations, AddressableAssetTree assetTree)
        {
            if (!string.IsNullOrEmpty(entry.address) && entry.address.Contains('[') && entry.address.Contains(']'))
            {
                Debug.LogErrorFormat("Address '{0}' cannot contain '[ ]'.", entry.address);
                return;
            }

            using (new AddressablesFileEnumerationScope(assetTree))
            {
                entry.GatherAllAssets(null, true, true, false, e =>
                {
                    if (e.IsScene)
                    {
                        if (type == typeof(object) || type == typeof(SceneInstance) || AddressableAssetUtility.MapEditorTypeToRuntimeType(e.MainAssetType, false) == type)
                            locations.Add(new ResourceLocationBase(e.address, e.AssetPath, ResourceProviderType.AssetDatabase, typeof(SceneInstance)));
                    }
                    else if ((type.IsAssignableFrom(e.MainAssetType) && type != typeof(object)))
                    {
                        locations.Add(new ResourceLocationBase(e.address, e.AssetPath, ResourceProviderType.AssetDatabase, e.MainAssetType));
                        return true;
                    }
                    else
                    {
                        ObjectIdentifier[] ids = ContentBuildInterface.GetPlayerObjectIdentifiersInAsset(new GUID(e.guid), EditorUserBuildSettings.activeBuildTarget);
                        if (ids.Length > 0)
                        {
                            foreach (var t in AddressableAssetEntry.GatherMainAndReferencedSerializedTypes(ids))
                            {
                                if (type.IsAssignableFrom(t))
                                    locations.Add(
                                        new ResourceLocationBase(e.address, e.AssetPath, ResourceProviderType.AssetDatabase, AddressableAssetUtility.MapEditorTypeToRuntimeType(t, false)));
                            }

                            return true;
                        }
                    }

                    return false;
                });
            }
        }

        static readonly List<IResourceLocation> _locationBuf = new();

        public bool Locate(string key, Type type, out IResourceLocation location)
        {
            Assert.IsNotNull(key, "Key cannot be null");
            Assert.IsFalse(string.IsNullOrEmpty(key), "Key cannot be empty");
            Assert.IsNotNull(type, "Type cannot be null");

            if (m_dirty)
                RebuildInternalData();

            var cacheKey = new CacheKey() {m_key = key, m_type = type};
            if (m_Cache.TryGetValue(cacheKey, out location))
                return location != null;

            if (m_keyToEntry.TryGetValue(key, out var e) is false)
                throw new ArgumentException("[AddressableAssetSettingsLocator] Unable to find key " + key);

            Assert.AreEqual(0, _locationBuf.Count, "Location buffer not empty");
            GatherEntryLocations(e, type, _locationBuf, m_AddressableAssetTree);
            Assert.AreEqual(1, _locationBuf.Count, "No locations found for key " + key);
            location = _locationBuf[0];
            _locationBuf.Clear();

            m_Cache.Add(cacheKey, location);
            return true;
        }
    }
}
