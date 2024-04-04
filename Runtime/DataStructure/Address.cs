using UnityEngine.AddressableAssets.Util;

namespace UnityEngine.AddressableAssets
{
    // Only use least significant 24 bits.
    public enum Address : uint
    {
    }

    public static class AddressUtils
    {
        public static Address Hash(string address)
        {
            // XXX: Just right after adding the entry, address will be null for AddressableAssetEntry.
            if (address is null) return default;

            var hash = Hasher.Hash(address) & 0xFFFFFF;
#if DEBUG
            Hasher.Debug_ForceAddReverseHash(hash, address);
#endif
            return (Address) (hash & 0xFFFFFF);
        }

        public static string Name(this Address hash)
        {
            return Hex.To6((uint) hash);
        }

        public static string ReadableString(this Address hash)
        {
#if DEBUG
            if (Debug_TryReverseHash(hash, out var address))
                return address;
#endif
            return Name(hash);
        }

#if DEBUG
        public static bool Debug_TryReverseHash(this Address hash, out string address)
            => Hasher.Debug_TryReverseHash((uint) hash, out address);
#endif
    }
}