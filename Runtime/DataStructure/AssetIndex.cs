namespace UnityEngine.AddressableAssets
{
    public enum AssetIndex : byte { }

    public readonly struct AssetLocation
    {
        public readonly AssetBundleId BundleId;
        public readonly AssetIndex AssetIndex;

        public AssetLocation(byte bundleMajor, byte bundleMinor, byte assetIndex)
        {
            BundleId = AssetBundleIdUtils.PackBundleId(bundleMajor, bundleMinor);
            AssetIndex = (AssetIndex) assetIndex;
        }

        public override string ToString()
        {
            return $"({BundleId.Name()}:{AssetIndex.Name()})";
        }
    }

    public static class AssetIndexUtils
    {
        public static string Name(this AssetIndex index)
        {
            return ((byte) index).ToStringSmallNumber();
        }
    }
}