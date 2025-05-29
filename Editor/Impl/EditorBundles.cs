using Object = UnityEngine.Object;

namespace Bundles.Editor
{
    public static class EditorBundles
    {
        public static bool Contains(string address)
        {
            return AssetCatalog.Default.ContainsEntry(address);
        }

        public static T LoadAsset<T>(Address address) where T : Object
        {
            return AssetCatalog.Default.GetEntry(address).LoadAssetWithType<T>();
        }
    }
}