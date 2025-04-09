using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets
{
    internal enum Address : uint { }

    internal static class AddressUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Value(this Address address)
        {
            return (uint) address;
        }

        public static Address Hash(string address)
        {
            return (Address) Hasher.Hash(address);
        }

        public static string Name(this Address address)
        {
            return Hex.To8(address.Value());
        }

        public static string ReadableString(this Address address)
        {
#if DEBUG
            if (Hasher.TryReverseHash(address.Value(), out var orgStr))
                return orgStr;
#endif
            return Name(address);
        }

        private static class Hasher
        {
            public static uint Hash(string str)
            {
                // customized djb2 hash
                var hash = 5381u;
                var len = str.Length;
                for (var i = 0; i < len; i++)
                {
                    var c = str[i];
                    Assert.IsTrue(c <= byte.MaxValue, $"Character is out of range: char={c} str={str}");
                    hash = ((hash << 5) + hash) + (byte) c;
                }

#if DEBUG
                AddReverseHash(hash, str);
#endif
                return hash;
            }

#if DEBUG
            private static readonly Dictionary<uint, string> _reverseHash = new();

            private static void AddReverseHash(uint hash, string str)
            {
                if (_reverseHash.TryGetValue(hash, out var existing))
                {
                    Assert.AreEqual(str, existing, $"Hash collision detected: {hash} -> {str} and {existing}");
                }
                else
                {
                    _reverseHash.Add(hash, str);
                }
            }

            internal static bool TryReverseHash(uint hash, out string str) =>
                _reverseHash.TryGetValue(hash, out str);
#endif
        }
    }
}