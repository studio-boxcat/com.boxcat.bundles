using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets
{
    public enum Address : uint { }

    public static class AddressUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint Value(this Address address)
        {
            return (uint) address;
        }

        public static Address Hash(string address)
        {
            // customized djb2 hash
            var hash = 5381u;
            var len = address.Length;
            for (var i = 0; i < len; i++)
            {
                var c = address[i];
                Assert.IsTrue(c <= byte.MaxValue, $"Character is out of range: char={c} str={address}");
                hash = ((hash << 5) + hash) + (byte) c;
            }
            return (Address) hash;
        }

        public static string Hex(this Address address)
        {
            return Util.Hex.To8(address.Value());
        }

#if DEBUG
        private static Dictionary<Address, string> _addressToString;
#endif

        public static string ReadableString(this Address address)
        {
#if DEBUG
            _addressToString ??= typeof(Addresses).GetFields().ToDictionary(
                field => (Address) field.GetValue(null),
                field => field.Name);
            if (_addressToString.TryGetValue(address, out var name))
                return name;
#endif
            return Hex(address);
        }
    }
}