using System.Collections.Generic;
using UnityEngine.AddressableAssets.AsyncOperations;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Entry point for ResourceManager API
    /// </summary>
    public class ResourceManager
    {
        readonly ResourceCatalog _catalog;
        readonly AssetBundleLoader _loader;
        readonly BundledAssetProvider _bundledAssetProvider = new();
        readonly SceneProvider _sceneProvider = new();
        readonly List<AssetOpBlock> _opBlockPool = new();


        public ResourceManager(ResourceCatalog catalog)
        {
            _catalog = catalog;
            _loader = new AssetBundleLoader(catalog.GetBundleCount());
        }

        public IAssetOp<TObject> LoadAsset<TObject>(string address) where TObject : Object
        {
            var b = GetOpBlock();
            InitOpBlock(b, address, _bundledAssetProvider);
            return new AssetOp<TObject>(b);
        }

        public IAssetOp<Scene> LoadScene(string address)
        {
            var b = GetOpBlock();
            InitOpBlock(b, address, _sceneProvider);
            return new AssetOp<Scene>(b);
        }

        AssetOpBlock GetOpBlock()
        {
            var count = _opBlockPool.Count;
            if (count == 0)
                return new AssetOpBlock(_catalog, _loader, _opBlockPool);

            var opBlock = _opBlockPool[count - 1];
            _opBlockPool.RemoveAt(count - 1);
            return opBlock;
        }

        void InitOpBlock(AssetOpBlock b, string address, IResourceProvider provider)
        {
            var addressHash = AddressUtils.Hash(address);
            var bundleId = _catalog.GetContainingBundle(addressHash);
            b.Init(addressHash, bundleId, provider);
        }
    }
}