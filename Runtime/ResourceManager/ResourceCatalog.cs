using System.Collections.Generic;
using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Represents a "view" over the portion of the catalog's dependency array
    /// corresponding to one bundle. Each dependency is now stored as a 2-byte (ushort) value,
    /// so this struct maps a [start, count] region in the ushort-based dependency array.
    /// </summary>
    public readonly struct AssetBundleSpan
    {
        private readonly byte[] _data;   // The entire catalog data buffer
        private readonly ushort _start;  // Index (in ushort units) of the first dependency
        private readonly ushort _count;  // How many dependencies

        public AssetBundleSpan(byte[] data, ushort start, ushort count)
        {
            _data = data;
            _start = start;
            _count = count;
        }

        public int Count
        {
            get
            {
                Assert.IsNotNull(_data, "Span not initialized");
                return _count;
            }
        }

        public override unsafe string ToString()
        {
            if (_count == 0) return string.Empty;

            var list = new List<string>(_count);
            fixed (byte* b = _data)
            {
                var depPtr = (ushort*) b;  // interpret entire _data as an array of ushorts
                for (int i = 0; i < _count; i++)
                {
                    var val = depPtr[_start + i];
                    list.Add(((AssetBundleId) val).Name());
                }
            }
            return string.Join(", ", list);
        }

        public unsafe AssetBundleId this[int index]
        {
            get
            {
                Assert.IsNotNull(_data, "Span not initialized");
                Assert.IsTrue(index >= 0 && index < _count, "Index out of range");
                fixed (byte* b = _data)
                {
                    var depPtr = (ushort*) b;
                    return (AssetBundleId) depPtr[_start + index];
                }
            }
        }

        public unsafe bool Contains(AssetBundleId bundle)
        {
            Assert.IsNotNull(_data, "Span not initialized");
            fixed (byte* b = _data)
            {
                var depPtr = (ushort*) b;
                var target = (ushort) bundle;
                for (int i = 0; i < _count; i++)
                {
                    if (depPtr[_start + i] == target)
                        return true;
                }
            }
            return false;
        }
    }

    internal readonly struct ResourceCatalog
    {
        // AssetBundleCount: ushort
        // AssetCount: ushort
        // AssetBundleDepSpans: uint[]
        //     Start: ushort
        //     Count: ushort
        // Addresses: uint[] (Sorted)
        // CorrespondingAssetBundles: ushort[] - AssetBundleIds
        // OptionalPadding: 0 or 2 bytes
        // AssetBundleDepData: ushort[] - AssetBundleIds
        private readonly byte[] _data;
        private readonly int _bundleCount;
        private readonly int _locCount;

        // Offsets (in bytes) of major sections
        private const int _depSpanOffset = 4;     // Start of AssetBundleDepSpans
        private readonly int _addressesOffset;   // Start of Addresses (uint[])
        private readonly int _bundleIdsOffset;   // Start of CorrespondingAssetBundles (ushort[])

        public unsafe ResourceCatalog(byte[] data)
        {
            _data = data;
            fixed (byte* b = data)
            {
                // Read the counts from the first 4 bytes (2 ushorts)
                var header = (ushort*) b;
                _bundleCount = header[0];
                _locCount = header[1];

                // The layout after these two ushorts is:
                //   depSpanOffset = 4 bytes in.
                //   size of DepSpans = bundleCount * 4 bytes (each is 2 ushorts)
                //   so addressesOffset = depSpanOffset + (bundleCount * 4)
                //
                //   then addresses array (uint[]) has _locCount elements => _locCount * 4 bytes
                //   then corresponding bundle IDs (ushort[]) has _locCount elements => _locCount * 2 bytes
                //   then possibly 2 bytes of padding if _locCount was odd
                //   then depData (2 bytes each)
                int baseOffset = _depSpanOffset; // Skip 2 ushorts = 4 bytes for bundleCount, locCount

                var depSpansSize = _bundleCount * 4;  // each span = 4 bytes
                baseOffset += depSpansSize;
                _addressesOffset = baseOffset;

                var addressesSize = _locCount * 4;    // each address = 4 bytes
                baseOffset += addressesSize;
                _bundleIdsOffset = baseOffset;
            }
        }

        public int GetBundleCount() => _bundleCount;

        /// <summary>
        /// Returns which bundle an Address belongs to by doing a binary search on the
        /// sorted Addresses array (uint[]). Once the matching index is found, we read
        /// the corresponding bundle ID from the separate ushort[] array.
        /// </summary>
        public unsafe AssetBundleId GetContainingBundle(Address address)
        {
            fixed (byte* b = _data)
            {
                // We read addresses as a uint[] starting at _addressesOffset
                var d = (uint*) (b + _addressesOffset);
                int l = 0;
                int r = _locCount - 1;
                var addr = address.Value();

                while (l <= r)
                {
                    var m = (l + r) >> 1;
                    var cur = d[m];
                    if (cur < addr) l = m + 1;
                    else if (cur > addr) r = m - 1;
                    else
                    {
                        // Found the address. The corresponding bundle ID is in the
                        // ushort array at index 'mid'.
                        var bundleIdsPtr = (ushort*) (b + _bundleIdsOffset);
                        var bundleId = bundleIdsPtr[m];
                        return (AssetBundleId) bundleId;
                    }
                }
            }
            throw new KeyNotFoundException(
                "Address not found in ResourceCatalog: " + address.ReadableString());
        }

        /// <summary>
        /// Retrieves the dependency span for a given bundle by reading the
        /// 'AssetBundleDepSpans' array. Each entry is 4 bytes:
        ///   [0..1] => Start (ushort)
        ///   [2..3] => Count (ushort)
        ///
        /// Then we construct an AssetBundleSpan pointing into the DepData section.
        /// </summary>
        public unsafe AssetBundleSpan GetDependencies(AssetBundleId bundle)
        {
            // AssetBundleDepSpans: uint[]
            //     Start: ushort
            //     Count: ushort
            var idx = bundle.Value();
            Assert.IsTrue(idx < _bundleCount, $"Invalid bundle index: {idx}");

            fixed (byte* b = _data)
            {
                // Each bundle's DepSpan is 4 bytes (2 ushorts).
                // So the offset in bytes for 'bundle' is _depSpanOffset + (idx * 4).
                var d = (ushort*) (b + _depSpanOffset + (idx * 4));

                // The 'start' is an index in the DepData (ushort[]) portion, i.e.
                // offset = _depDataOffset + 2 * start
                // but we only need to store it in the AssetBundleSpan. We'll interpret it
                // at runtime via pointer arithmetic.
                return new AssetBundleSpan(_data, d[0], d[1]);
            }
        }
    }
}
