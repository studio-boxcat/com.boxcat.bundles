using System.Collections.Generic;
using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets.Util
{
    public class Hasher
    {
        public static uint Hash(string str)
        {
            ulong hash = 5381;
            var i = 0;
            for (i = 0; i < str.Length; i++)
                hash = ((hash << 5) + hash) + ((byte) str[i]);

            var finalHash = (uint) hash.GetHashCode();
#if DEBUG
            Debug_AddReverseHash(finalHash, str);
#endif
            return finalHash;
        }

#if DEBUG
        static readonly Dictionary<uint, string> _debug_HashToStr = new();

        static void Debug_AddReverseHash(uint hash, string str)
        {
            if (_debug_HashToStr.TryGetValue(hash, out var existing))
            {
                Assert.AreEqual(str, existing, $"Hash collision detected: {hash} -> {str} and {existing}");
            }
            else
            {
                _debug_HashToStr.Add(hash, str);
            }
        }

        internal static bool Debug_TryReverseHash(uint hash, out string str)
        {
            return _debug_HashToStr.TryGetValue(hash, out str);
        }

        internal static void Debug_ForceAddReverseHash(uint hash, string str)
        {
            Debug_AddReverseHash(hash, str);
        }
#endif
    }
}