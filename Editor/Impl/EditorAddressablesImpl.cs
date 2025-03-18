using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.AsyncOperations;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace UnityEditor.AddressableAssets
{
    [UsedImplicitly]
    internal class EditorAddressablesImpl : IAddressablesImpl
    {
        private static readonly List<EditorAddressablesImpl> _cache = new();

        [EditorAddressablesImplFactory, UsedImplicitly]
        private static EditorAddressablesImpl CreateImpl([CanBeNull] AddressableCatalog catalog)
        {
            // Use default catalog if none provided.
            catalog ??= AddressableCatalog.Default;

            // Reuse existing instance if catalog are the same.
            var impl = _cache.Find(i => i._catalog == catalog);
            if (impl is not null) return impl;

            // Create new instance.
            impl = new EditorAddressablesImpl(catalog);
            _cache.Add(impl);
            return impl;
        }


        private readonly GuidMap _guidMap;
        private readonly AddressableCatalog _catalog;
        private bool _dirty;


        public EditorAddressablesImpl(AddressableCatalog catalog)
        {
            _guidMap = new GuidMap();
            _guidMap.RebuildInternalData(catalog.groups);
            _catalog = catalog;
            _catalog.OnModification += Settings_OnModification;
        }

        public IAssetOp<TObject> LoadAssetAsync<TObject>(string address) where TObject : Object
        {
            RebuildInternalData();
            var path = AssetDatabase.GUIDToAssetPath(_guidMap[address].Value);
            return new EditorAssetOp<TObject>(path);
        }

        public TObject LoadAsset<TObject>(string address) where TObject : Object
        {
            return AddressablesUtils.Load<TObject>(_guidMap[address]);
        }

        public IAssetOp<Scene> LoadSceneAsync(string address)
        {
            RebuildInternalData();
            return new EditorSceneOp(_guidMap[address]);
        }

        private void RebuildInternalData()
        {
            if (_dirty is false) return;
            _guidMap.RebuildInternalData(_catalog.groups);
            _dirty = false;
        }

        private void Settings_OnModification(AddressableCatalog catalog, AddressableCatalog.ModificationEvent evt, object arg3)
        {
            switch (evt)
            {
                case AddressableCatalog.ModificationEvent.EntryAdded:
                case AddressableCatalog.ModificationEvent.EntryCreated:
                case AddressableCatalog.ModificationEvent.EntryModified:
                case AddressableCatalog.ModificationEvent.EntryMoved:
                case AddressableCatalog.ModificationEvent.EntryRemoved:
                case AddressableCatalog.ModificationEvent.GroupRemoved:
                case AddressableCatalog.ModificationEvent.BatchModification:
                    _dirty = true;
                    break;
            }
        }

        private class GuidMap
        {
            private readonly Dictionary<string, AddressableAssetEntry> _addressToEntry = new();

            public AssetGUID this[string address] => Map(address);

            private AssetGUID Map(string address)
            {
                Assert.IsNotNull(address, "Address cannot be null");
                Assert.IsFalse(string.IsNullOrEmpty(address), "Address cannot be empty");

                var found = _addressToEntry.TryGetValue(address, out var e);
                Assert.IsTrue(found, "Address not found: " + address);
                Assert.AreEqual(address, e.address, $"Address mismatch: {address} != {e.address}");

                return e.guid;
            }

            public void RebuildInternalData(List<AddressableAssetGroup> groups)
            {
                _addressToEntry.Clear();

                foreach (var g in groups)
                {
                    if (g == null) continue;
                    foreach (var e in g.entries)
                    {
                        // Empty address means it's only for including this asset into the group.
                        if (string.IsNullOrEmpty(e.address)) continue;
                        var added = _addressToEntry.TryAdd(e.address, e);
                        if (added is false) L.E("Address already exists: " + e.address, e.MainAsset);
                    }
                }
            }
        }
    }
}