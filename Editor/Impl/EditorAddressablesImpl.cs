using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace UnityEditor.AddressableAssets
{
    internal class EditorAddressablesImpl : IAddressablesImpl
    {
        private readonly AddressableCatalog _catalog;


        public EditorAddressablesImpl(AddressableCatalog catalog)
        {
            _catalog = catalog;
        }

        public IAssetOp<TObject> LoadAssetAsync<TObject>(string address) where TObject : Object =>
            CreateAssetOp<TObject>(GetEntryByAddress(address));

        public TObject LoadAsset<TObject>(string address) where TObject : Object =>
            (TObject) GetEntryByAddress(address).Asset;

        public IAssetOp<TObject> LoadAssetAsync<TObject>(AssetLocation loc) where TObject : Object =>
            CreateAssetOp<TObject>(GetEntryByLocation(loc));

        public TObject LoadAsset<TObject>(AssetLocation loc) where TObject : Object =>
            (TObject) GetEntryByLocation(loc).Asset;

        public IAssetOp<Scene> LoadSceneAsync(string address) =>
            new EditorSceneOp(GetEntryByAddress(address).GUID);

        private AssetEntry GetEntryByAddress(string address) => _catalog.GetEntryByAddress(address);
        private AssetEntry GetEntryByLocation(AssetLocation loc) => _catalog.GetGroup(loc.BundleId)[loc.AssetIndex];

        private static IAssetOp<TObject> CreateAssetOp<TObject>(AssetEntry entry) where TObject : Object =>
            new EditorAssetOp<TObject>(entry.ResolveAssetPath());
    }
}