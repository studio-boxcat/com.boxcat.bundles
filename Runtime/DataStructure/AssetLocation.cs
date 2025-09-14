namespace Bundles
{
    public enum AssetIndex : ushort { }

    public readonly struct AssetLocation
    {
        public readonly AssetBundleId BundleId;
        public readonly AssetIndex AssetIndex;

        public AssetLocation(AssetBundleMajor bundleMajor, byte bundleMinor, ushort assetIndex)
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
        public static ushort Val(this AssetIndex index) => (ushort) index;
        public static string Name(this AssetIndex index) => index.Val().Strm();

        public static AssetLocation Locate(this AssetBundleMajor major, byte minor, byte index) =>
            new(major, minor, index);
        public static AssetLocation Locate0(this AssetBundleMajor major, byte minor) =>
            new(major, minor, 0);
    }
}