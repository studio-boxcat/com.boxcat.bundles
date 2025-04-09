using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.AsyncOperations;
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
            var impl = _cache.Find(i => ReferenceEquals(i._catalog, catalog));
            if (impl is not null) return impl;

            // Create new instance.
            impl = new EditorAddressablesImpl(catalog);
            _cache.Add(impl);
            return impl;
        }


        private readonly AddressableCatalog _catalog;


        public EditorAddressablesImpl(AddressableCatalog catalog)
        {
            _catalog = catalog;
        }

        private AssetEntry GetEntryByAddress(string address)
        {
            return _catalog.GetEntryByAddress(address);
        }

        public IAssetOp<TObject> LoadAssetAsync<TObject>(string address) where TObject : Object
        {
            var entry = GetEntryByAddress(address);
            var path = AssetDatabase.GUIDToAssetPath(entry.ResolveAssetPath());
            return new EditorAssetOp<TObject>(path);
        }

        public TObject LoadAsset<TObject>(string address) where TObject : Object
        {
            return (TObject) GetEntryByAddress(address).Asset;
        }

        public IAssetOp<Scene> LoadSceneAsync(string address)
        {
            return new EditorSceneOp(GetEntryByAddress(address).GUID);
        }
    }
}