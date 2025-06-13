using Object = UnityEngine.Object;

namespace Bundles.Editor
{
    public static class EditorBundles
    {
        public static bool Contains(string address)
        {
            return AssetCatalog.Default.ContainsEntry(AddressUtils.Hash(address));
        }

        public static string GetGUID(AssetLocation address)
        {
            return AssetCatalog.Default.GetEntry(address).GUID.Value;
        }

        public static T LoadAsset<T>(Address address) where T : Object
        {
            return AssetCatalog.Default.GetEntry(address).LoadAssetWithType<T>();
        }
    }
}