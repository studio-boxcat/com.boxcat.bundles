namespace UnityEngine.AddressableAssets
{
    public readonly struct ResourceLocation
    {
        public readonly Address Address; // 3 bytes
        public readonly AssetBundleId AssetBundle; // 1 byte


        public ResourceLocation(Address address, AssetBundleId assetBundle)
        {
            Address = address;
            AssetBundle = assetBundle;
        }

        public override string ToString() => Address.ReadableString();
    }
}