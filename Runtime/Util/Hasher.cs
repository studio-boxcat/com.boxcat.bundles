using System.Collections.Generic;
using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets.Util
{
    public static class Hasher
    {
        public static uint Hash(string str)
        {
            uint hash = 5381;
            var len = str.Length;
            for (var i = 0; i < len; i++)
            {
                var c = str[i];
                Assert.IsTrue(c <= byte.MaxValue, $"Character is out of range: char={c} str={str}");
                hash = ((hash << 5) + hash) + (byte) c;
            }

#if DEBUG
            Debug_AddReverseHash(hash, str);
#endif
            return hash;
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