using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine.Assertions;

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
        public bool Equals(Address x, Address y) => x.Value() == y.Value();
        public int GetHashCode(Address obj) => (int) obj.Value();
    }

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

        internal static string Hex(this Address address)
        {
            return global::Hex.To8(address.Value());
        }

        public static string Name(this Address address)
        {
            return address.ToString();
        }
    }
}