namespace Bundles
{
    public enum AssetIndex : uint { }

    public readonly struct AssetLocation
    {
        public readonly AssetBundleId BundleId;
        public readonly AssetIndex AssetIndex;

        public AssetLocation(AssetBundleMajor bundleMajor, byte bundleMinor, uint assetIndex)
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
        public static uint Val(this AssetIndex index) => (uint) index;

        public static string Name(this AssetIndex index) => ((int) index).StrSmall();

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