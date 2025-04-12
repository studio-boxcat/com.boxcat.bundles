namespace UnityEngine.AddressableAssets
{
    public enum AssetIndex : byte { }

    public readonly struct AssetLocation
    {
        public readonly AssetBundleId BundleId;
        public readonly AssetIndex AssetIndex;

        public AssetLocation(AssetBundleMajor bundleMajor, byte bundleMinor, byte assetIndex)
        {
            BundleId = AssetBundleIdUtils.PackBundleId(bundleMajor, bundleMinor);
            AssetIndex = (AssetIndex) assetIndex;
        }

        public override string ToString()
        {
            return $"${BundleId.Name()}:{AssetIndex.Name()}";
        }
    }

    public static class AssetIndexUtils
    {
        public static string Name(this AssetIndex index)
        {
            return ((byte) index).ToStringSmallNumber();
        }

        public static AssetLocation Locate(this AssetBundleMajor major, byte minor, byte index)
        {
            return new AssetLocation(major, minor, index);
        }

        public static AssetLocation Locate0(this AssetBundleMajor major, byte minor)
        {
            return new AssetLocation(major, minor, 0);
        }
    }
}