using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.AsyncOperations;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace UnityEditor.AddressableAssets.Settings
{
    [UsedImplicitly]
    class EditorAddressablesImpl : IAddressablesImpl
    {
        static readonly List<EditorAddressablesImpl> _cache = new();

        [EditorAddressablesImplFactory, UsedImplicitly]
        static EditorAddressablesImpl CreateImpl([CanBeNull] AddressableAssetSettings settings)
        {
            // Use default settings if none provided.
            settings ??= AddressableAssetSettingsDefaultObject.Settings;

            // Reuse existing instance if settings are the same.
            var impl = _cache.Find(i => i._settings == settings);
            if (impl is not null) return impl;

            // Create new instance.
            impl = new EditorAddressablesImpl(settings);
            _cache.Add(impl);
            return impl;
        }


        readonly PathMap _pathMap;
        readonly AddressableAssetSettings _settings;
        bool _dirty;


        public EditorAddressablesImpl(AddressableAssetSettings settings)
        {
            _pathMap = new PathMap();
            _pathMap.RebuildInternalData(settings.groups);
            _settings = settings;
            _settings.OnModification += Settings_OnModification;
        }

        public IAssetOp<TObject> LoadAssetAsync<TObject>(string address) where TObject : Object
        {
            RebuildInternalData();
            return new EditorAssetOp<TObject>(_pathMap[address]);
        }

        public TObject LoadAsset<TObject>(string address) where TObject : Object
        {
            return AssetDatabase.LoadAssetAtPath<TObject>(_pathMap[address]);
        }

        public IAssetOp<Scene> LoadSceneAsync(string address)
        {
            RebuildInternalData();
            return new EditorSceneOp(_pathMap[address]);
        }

        void RebuildInternalData()
        {
            if (_dirty is false) return;
            _pathMap.RebuildInternalData(_settings.groups);
            _dirty = false;
        }

        void Settings_OnModification(AddressableAssetSettings settings, AddressableAssetSettings.ModificationEvent evt, object arg3)
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
                    _dirty = true;
                    break;
            }
        }

        class PathMap
        {
            readonly Dictionary<string, AddressableAssetEntry> _addressToEntry = new();

            public string this[string address] => Map(address);

            string Map(string address)
            {
                Assert.IsNotNull(address, "Address cannot be null");
                Assert.IsFalse(address.Contains('[') || address.Contains(']'), "Address cannot contain '[ ]': " + address);
                Assert.IsFalse(string.IsNullOrEmpty(address), "Address cannot be empty");

                var found = _addressToEntry.TryGetValue(address, out var e);
                Assert.IsTrue(found, "Address not found: " + address);
                Assert.AreEqual(address, e.address, $"Address mismatch: {address} != {e.address}");

                return e.AssetPath;
            }

            public void RebuildInternalData(List<AddressableAssetGroup> groups)
            {
                _addressToEntry.Clear();

                foreach (var g in groups)
                {
                    if (g == null) continue;
                    foreach (var e in g.entries)
                    {
                        var added = _addressToEntry.TryAdd(e.address, e);
                        if (added is false) L.E(e.MainAsset, "Address already exists: " + e.address);
                    }
                }
            }
        }
    }
}