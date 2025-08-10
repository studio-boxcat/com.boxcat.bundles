using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;

namespace Bundles
{
    [Serializable, InlineProperty]
    public struct AddressWrap
    {
        [HideLabel]
        public Address Value;
        public AddressWrap(Address value) => Value = value;
        public override string ToString() => Value.Name();
        public static implicit operator Address(AddressWrap value) => value.Value;
        public static implicit operator AddressWrap(Address value) => new(value);
    }

    public class AddressComparer : IEqualityComparer<Address>
    {
        public static readonly AddressComparer Instance = new();
        public bool Equals(Address x, Address y) => x.Val() == y.Val();
        public int GetHashCode(Address obj) => (int) obj.Val();
    }

    public static class AddressUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint Val(this Address address)
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
#if DEBUG
                if (c > byte.MaxValue)
                    throw new Exception($"Character is out of range: char={c} str={address}");
#endif
                hash = ((hash << 5) + hash) + (byte) c;
            }
            return (Address) hash;
        }

        internal static string Hex(this Address address)
        {
            return global::Hex.To8(address.Val());
        }

        public static string Name(this Address address)
        {
            return address.ToString();
        }
    }
}