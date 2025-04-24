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

        public IAssetOp<TObject> LoadAssetAsync<TObject>(Address address) where TObject : Object =>
            CreateAssetOp<TObject>(GetEntryByAddress(address));

        public TObject LoadAsset<TObject>(Address address) where TObject : Object =>
            GetEntryByAddress(address).LoadAssetWithType<TObject>();

        public IAssetOp<TObject> LoadAssetAsync<TObject>(AssetLocation loc) where TObject : Object =>
            CreateAssetOp<TObject>(GetEntryByLocation(loc));

        public TObject LoadAsset<TObject>(AssetLocation loc) where TObject : Object =>
            GetEntryByLocation(loc).LoadAssetWithType<TObject>();

        public IAssetOp<Scene> LoadSceneAsync(Address address) =>
            new EditorSceneOp(GetEntryByAddress(address).GUID);

        private AssetEntry GetEntryByAddress(Address address) => _catalog.GetEntry(address);
        private AssetEntry GetEntryByLocation(AssetLocation loc) => _catalog.GetGroup(loc.BundleId)[loc.AssetIndex];

        private static IAssetOp<TObject> CreateAssetOp<TObject>(AssetEntry entry) where TObject : Object
        {
            return new EditorAssetOp<TObject>(entry.ResolveAssetPath(), SimulateDelay());

            static float SimulateDelay()
            {
                if (Application.isPlaying is false) return 0f;
                if (EditorConfig.NoAssetDatabaseDelaySimulation) return 0f;
                var noDelay = Random.value < 0.05f; // 5% chance of no delay.
                if (noDelay) return 0f;
                var loadDelay = Random.Range(0.05f, 0.15f); // 0.05s - 0.15s delay.
                return loadDelay;
            }
        }
    }
}