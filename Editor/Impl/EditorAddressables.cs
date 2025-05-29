using Object = UnityEngine.Object;

namespace Bundles.Editor
{
    public static class EditorAddressables
    {
        public static bool Contains(string address)
        {
            return AddressableCatalog.Default.ContainsEntry(address);
        }

        public static T LoadAsset<T>(Address address) where T : Object
        {
            return AddressableCatalog.Default.GetEntry(address).LoadAssetWithType<T>();
        }
    }
}