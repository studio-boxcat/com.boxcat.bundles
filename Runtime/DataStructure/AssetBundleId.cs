using System.Runtime.CompilerServices;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets
{
    public enum AssetBundleId : ushort
    {
        MonoScript = 0, // reserved for MonoScript.
    }

    // canonical index of AssetBundles in the ResourceCatalog
    internal enum AssetBundleIndex : ushort
    {
        MonoScript = 0,
    }

    public static class AssetBundleIdUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Value(this AssetBundleId id)
        {
            return (ushort) id;
        }

        public static void WriteHex4(this AssetBundleId id, char[] chars, int startIndex)
        {
            Hex.To4(id.Value(), chars, startIndex);
        }

        public static string Name(this AssetBundleId id)
        {
            return Hex.To4(id.Value());
        }

#if UNITY_EDITOR
        public static AssetBundleId Parse(string name)
        {
            return (AssetBundleId) Hex.Parse4(name);
        }
#endif

        public static AssetBundleId MaxForNormalBundle()
        {
            return (AssetBundleId) ((1 << 10) - 1);
        }

        public static bool AddressAccess(this AssetBundleId id) => ((int) id >> 10) == 0; // MSB 6 bits = bundleMajor

        public static AssetBundleId PackBundleId(AssetBundleMajor bundleMajor, byte bundleMinor)
        {
            Assert.IsTrue(bundleMajor is not 0, "bundleMajor must be greater than 0");
            Assert.IsTrue((int) bundleMajor < (1 << 6), "bundleMajor must be less than 64"); // MSB 6 bits
            return (AssetBundleId) (((int) bundleMajor << 10) | bundleMinor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort Value(this AssetBundleIndex id) => (ushort) id;

        internal static string DebugString(this AssetBundleIndex id) => "#" + ((int) id).D3();
    }
}