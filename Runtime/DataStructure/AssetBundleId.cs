using System.Runtime.CompilerServices;
using UnityEngine.AddressableAssets.Util;

namespace UnityEngine.AddressableAssets
{
    public enum AssetBundleId : ushort
    {
        MonoScript = 0, // reserved for MonoScript.
        BuiltInShader = 1, // reserved for BuiltInShader.
        Max = ushort.MaxValue,
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

        public static int CompareToFast(this AssetBundleId id, AssetBundleId other)
        {
            return id.Value().CompareTo(other.Value());
        }
    }
}