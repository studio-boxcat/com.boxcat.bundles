using Object = UnityEngine.Object;

namespace Bundles.Editor
{
    public static class EditorBundles
    {
        private static AssetCatalog _catalog => AssetCatalog.Default;

        public static bool Contains(string address) =>
            _catalog.ContainsEntry(AddressUtils.Hash(address));
        public static string GetGUID(AssetLocation address) =>
            _catalog.GetEntry(address).GUID.Value;
        public static T LoadAsset<T>(Address address) where T : Object =>
            _catalog.GetEntry(address).LoadAssetWithType<T>();
        public static T LoadAsset<T>(AssetLocation loc) where T : Object =>
            _catalog.GetEntry(loc).LoadAssetWithType<T>();
    }
}