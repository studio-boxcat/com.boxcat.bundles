using System.Collections.Generic;

namespace Bundles
{
    public class AddressComparer : IEqualityComparer<Address>
    {
        public static readonly AddressComparer Instance = new();
        public bool Equals(Address x, Address y) => x.Value() == y.Value();
        public int GetHashCode(Address obj) => (int) obj.Value();
    }
}