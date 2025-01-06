using System.Collections.Generic;
using UnityEngine.AddressableAssets.AsyncOperations;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Entry point for ResourceManager API
    /// </summary>
    internal class AddressablesImpl : IAddressablesImpl
    {
        private readonly ResourceCatalog _catalog;
        private readonly AssetBundleLoader _loader;
        private readonly BundledAssetProvider _bundledAssetProvider = new();
        private readonly SceneProvider _sceneProvider = new();
        private readonly List<AssetOpBlock> _opBlockPool = new();


        public AddressablesImpl(ResourceCatalog catalog)
        {
            _catalog = catalog;
            _loader = new AssetBundleLoader(catalog.GetBundleCount());
        }

        public IAssetOp<TObject> LoadAssetAsync<TObject>(string address) where TObject : Object
        {
            var b = GetOpBlock(address, _bundledAssetProvider);
            return new AssetOp<TObject>(b);
        }

        public TObject LoadAsset<TObject>(string address) where TObject : Object
        {
            var addressHash = AddressUtils.Hash(address);
            var bundleId = _catalog.GetContainingBundle(addressHash);
            if (_loader.TryGetResolvedBundle(bundleId, out var bundle) is false)
                bundle = _loader.ResolveImmediate(bundleId, _catalog.GetDependencies(bundleId));
            return bundle.LoadAsset<TObject>(addressHash.Name());
        }

        public IAssetOp<Scene> LoadSceneAsync(string address)
        {
            var b = GetOpBlock(address, _sceneProvider);
            return new AssetOp<Scene>(b);
        }

        private AssetOpBlock GetOpBlock(string address, IResourceProvider provider)
        {
            var count = _opBlockPool.Count;

            AssetOpBlock b;
            if (count is 0)
            {
                b = new AssetOpBlock(_catalog, _loader, _opBlockPool);
            }
            else
            {
                b = _opBlockPool[count - 1];
                _opBlockPool.RemoveAt(count - 1);
            }

            var addressHash = AddressUtils.Hash(address);
            var bundleId = _catalog.GetContainingBundle(addressHash);
            b.Init(addressHash, bundleId, provider);
            return b;
        }
    }
}