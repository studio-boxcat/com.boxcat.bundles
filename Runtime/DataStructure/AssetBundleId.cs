using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine.AddressableAssets.Util;

namespace UnityEngine.AddressableAssets
{
    public enum AssetBundleId : byte
    {
        MonoScript = 0, // reserved for MonoScript.
    }

    public static class AssetBundleIdUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Index(this AssetBundleId id)
        {
            return (byte) id;
        }

        public static void WriteHex2(this AssetBundleId id, StringBuilder sb, int startIndex)
        {
            sb[startIndex] = Hex.Char(((byte) id >> 4) & 0xF);
            sb[startIndex + 1] = Hex.Char((byte) id & 0xF);
        }

        public static string Name(this AssetBundleId id)
        {
            return Hex.To2((byte) id);
        }

        public static AssetBundleId Parse(string name)
        {
            return (AssetBundleId) Hex.Parse2(name);
        }
    }
}