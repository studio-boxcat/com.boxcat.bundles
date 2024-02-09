using System;
using System.Collections.Generic;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Assertions;
using UnityEngine.ResourceManagement.ResourceLocations;
using static UnityEditor.AddressableAssets.Settings.AddressablesFileEnumeration;

namespace UnityEditor.AddressableAssets.Settings
{
    internal class AddressableAssetSettingsLocator : IResourceLocator
    {
        public Dictionary<string, AddressableAssetEntry> m_keyToEntry;
        public AddressableAssetTree m_AddressableAssetTree;
        AddressableAssetSettings m_Settings;
        bool m_dirty = true;

        public AddressableAssetSettingsLocator(AddressableAssetSettings settings)
        {
            m_Settings = settings;
            m_dirty = true;
            m_Settings.OnModification += Settings_OnModification;
        }

        void RebuildInternalData()
        {
            m_AddressableAssetTree = BuildAddressableTree(m_Settings);
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

        public IResourceLocation Locate(string key, Type type)
        {
            Assert.IsNotNull(key, "Address cannot be null");
            Assert.IsFalse(key.Contains('[') || key.Contains(']'), "Address cannot contain '[ ]': " + key);
            Assert.IsFalse(string.IsNullOrEmpty(key), "Address cannot be empty");
            Assert.IsNotNull(type, "Type cannot be null");
            Assert.IsTrue(typeof(UnityEngine.Object).IsAssignableFrom(type));

            if (m_dirty)
                RebuildInternalData();

            if (m_keyToEntry.TryGetValue(key, out var e) is false)
                throw new ArgumentException("[AddressableAssetSettingsLocator] Unable to find key " + key);
            Assert.AreEqual(key, e.address, "Address mismatch for key " + key);

            var resourceType =  AddressableAssetUtility.MapEditorTypeToRuntimeType(e.MainAssetType);
            Assert.IsTrue(type.IsAssignableFrom(resourceType), "Type " + type + " is not assignable from " + resourceType);
            return new ResourceLocationBase(e.address, e.AssetPath, ResourceProviderType.AssetDatabase, resourceType);
        }
    }
}
