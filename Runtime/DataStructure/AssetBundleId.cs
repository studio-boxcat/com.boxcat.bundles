using System.Runtime.CompilerServices;
using UnityEngine.Assertions;

namespace Bundles
{
    // canonical index of AssetBundles in the ResourceCatalog
    internal enum AssetBundleIndex : ushort
    {
        MonoScript = 0,
    }

    public static class AssetBundleIdUtils
    {
        public static byte Val(this AssetBundleMajor major) => (byte) major;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Val(this AssetBundleId id) => (ushort) id;
        public static string Name(this AssetBundleId id) => Hex.To4(id.Val());

#if UNITY_EDITOR
        public static AssetBundleId Parse(string name) => (AssetBundleId) Hex.Parse4(name);
#endif

        public static AssetBundleId MaxForNormalBundle() => (AssetBundleId) ((1 << 10) - 1);

        public static bool AddressAccess(this AssetBundleId id) => (id.Val() >> 10) == 0; // MSB 6 bits = bundleMajor

        public static AssetBundleId PackBundleId(AssetBundleMajor bundleMajor, byte bundleMinor)
        {
            Assert.IsTrue(bundleMajor is not 0, "bundleMajor must be greater than 0");
            Assert.IsTrue(bundleMajor.Val() < (1 << 6), "bundleMajor must be less than 64"); // MSB 6 bits
            return (AssetBundleId) ((bundleMajor.Val() << 10) | bundleMinor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort Val(this AssetBundleIndex id) => (ushort) id;

        internal static string DebugString(this AssetBundleIndex id)
        {
            return string.Create(4, id.Val(), static (s, v) =>
            {
                s[0] = '#';
                v.D3(out s[1], out s[2], out s[3]);
            });
        }
    }
}