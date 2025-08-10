using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;
using UnityEngine.Networking;

namespace Bundles
{
    // AssetBundleCount: ushort
    // AssetCount: ushort
    // Addresses: uint[] - sorted
    // AssetBundleDepSpans: uint[] - indexed by AssetBundleCanonicalIndex
    //     Start: ushort
    //     Count: ushort
    // CorrespondingAssetBundles: ushort[] - AssetBundleCanonicalIndex, 1 to 1 with Addresses
    // AssetBundleIds: ushort[] - sorted
    // AssetBundleDepData: ushort[] - list of AssetBundleIds, index=AssetBundleIndex
    internal class ResourceCatalog
    {
        private readonly IDisposable _buf;
        private readonly NativeArray<byte>.ReadOnly _data;

        private readonly int _bundleCount;
        private readonly int _assetCount;

        private const int _addressesOffset = 4;
        private readonly int _depSpanOffset;
        private readonly int _correspondingBundlesOffset;
        private readonly int _assetBundleIdsOffset;

        public readonly IndexToId IndexToId;

        private unsafe ResourceCatalog(DownloadHandlerBuffer buf)
        {
            _buf = buf;
            _data = buf.nativeData;

            // Read the counts from the first 4 bytes (2 ushorts)
            var b = (byte*) _data.GetUnsafeReadOnlyPtr();
            var header = (ushort*) b;
            _bundleCount = header[0];
            _assetCount = header[1];

            var offset = 4; // skip 2 ushorts

            // 1) Addresses
            var addressesSize = _assetCount * 4; // each address is 4 bytes
            offset += addressesSize;

            // 2) DepSpans
            _depSpanOffset = offset;
            var depSpanSize = _bundleCount * 4; // each span is [Start, Count] => 2 ushorts = 4 bytes
            offset += depSpanSize;

            // 3) CorrespondingAssetBundles
            _correspondingBundlesOffset = offset;
            var corrBundlesSize = _assetCount * 2; // each is 1 ushort
            offset += corrBundlesSize;

            // 4) AssetBundleIds
            _assetBundleIdsOffset = offset;
            IndexToId = new IndexToId(_data, _assetBundleIdsOffset);

            L.I("[ResourceCatalog] data=" + _data.Length + " bytes\n"
                + "-- header --\n"
                + "  AssetBundleCount: " + _bundleCount + "\n"
                + "  AssetCount: " + _assetCount + "\n"
                + "-- sizes --\n"
                + "  Addresses: " + addressesSize + " bytes\n"
                + "  AssetBundleDepSpans: " + depSpanSize + " bytes\n"
                + "  CorrespondingAssetBundles: " + corrBundlesSize + " bytes\n"
                + "  AssetBundleDepData: " + (_data.Length - offset) + " bytes\n"
                + "-- offsets --\n"
                + "  DepSpans offset: " + _depSpanOffset + "\n"
                + "  CorrespondingAssetBundles offset: " + _correspondingBundlesOffset + "\n"
                + "  AssetBundleIds offset: " + _assetBundleIdsOffset + "\n");
        }

        public void Dispose()
        {
            _buf.Dispose();
        }

        public int GetBundleCount() => _bundleCount;

        public unsafe AssetBundleIndex GetBundleIndex(AssetBundleId id)
        {
            // We read AssetBundleIds as a ushort[] starting at _assetBundleIdsOffset
            var b = (byte*) _data.GetUnsafeReadOnlyPtr();
            var d = (ushort*) (b + _assetBundleIdsOffset);
            var v = id.Value();

            int l = 0;
            int r = _bundleCount - 1;
            while (l <= r)
            {
                var m = (l + r) >> 1;
                var cur = d[m];
                if (cur < v) l = m + 1;
                else if (cur > v) r = m - 1;
                else return (AssetBundleIndex) m; // Found the index
            }

            throw new KeyNotFoundException("AssetBundleId not found in ResourceCatalog: " + id.Name());
        }

        /// <summary>
        /// Binary searches the sorted Addresses[] (uint) for the given Address,
        /// then uses the same index to pick a canonical bundle index out of
        /// CorrespondingAssetBundles[].
        /// Finally, if you truly need the *raw* <see cref="AssetBundleId"/>,
        /// you must do a second lookup from AssetBundleIds[]:
        ///
        ///   rawId = assetBundleIdsPtr[canonicalIndex];
        ///   return (AssetBundleId) rawId;
        ///
        /// For minimal changes below, we just return the canonical index
        /// cast to AssetBundleId. You can adapt as needed.
        /// </summary>
        public unsafe AssetBundleIndex GetContainingBundle(Address address)
        {
            // We read addresses as a uint[] starting at _addressesOffset
            var b = (byte*) _data.GetUnsafeReadOnlyPtr();
            var d = (uint*) (b + _addressesOffset);
            var v = address.Val();

            int l = 0;
            int r = _assetCount - 1;
            while (l <= r)
            {
                var m = (l + r) >> 1;
                var cur = d[m];
                if (cur < v) l = m + 1;
                else if (cur > v) r = m - 1;
                else // Found the address
                {
                    var cPtr = (AssetBundleIndex*) (b + _correspondingBundlesOffset);
                    return cPtr[m];
                }
            }

            throw new KeyNotFoundException("Address not found in ResourceCatalog: " + address.Name());
        }

        /// <summary>
        /// Retrieves the dependency span for a given bundle by reading the
        /// 'AssetBundleDepSpans' array. Each entry is 4 bytes:
        ///   [0..1] => Start (ushort)
        ///   [2..3] => Count (ushort)
        ///
        /// Then we construct an AssetBundleSpan pointing into the DepData section.
        /// </summary>
        public unsafe DepSpan GetDependencies(AssetBundleIndex bundleIndex)
        {
            var idx = (int) bundleIndex;
            Assert.IsTrue(idx < _bundleCount, $"Invalid bundle index: {idx}");

            // Each bundle's DepSpan is 4 bytes (2 ushorts).
            // So the offset in bytes for 'bundle' is _depSpanOffset + (idx * 4).
            var b = (byte*) _data.GetUnsafeReadOnlyPtr();
            var d = (ushort*) (b + _depSpanOffset + (idx * 4));

            // The 'start' is an index in the DepData (ushort[]) portion, i.e.
            // offset = _depDataOffset + 2 * start
            // but we only need to store it in the AssetBundleSpan. We'll interpret it
            // at runtime via pointer arithmetic.
            return new DepSpan(_data, d[0], d[1]);
        }

        public static ResourceCatalog Load(string uri)
        {
#if DEBUG
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
#endif

            L.I("[ResourceCatalog] Load: " + uri);

            var buf = new DownloadHandlerBuffer();
            using var req = new UnityWebRequest(uri, "GET", buf, null)
            {
                disposeDownloadHandlerOnDispose = false // we'll dispose it ourselves
            };
            req.SendWebRequest().WaitForComplete(); // complete immediately
            Assert.AreEqual(req.result, UnityWebRequest.Result.Success, "Failed to load catalog.");
            var catalog = new ResourceCatalog(buf);

#if DEBUG
            sw.Stop();
            L.I("[ResourceCatalog] Load done: " + sw.ElapsedMilliseconds + "ms");
#endif

            return catalog;
        }
    }
}