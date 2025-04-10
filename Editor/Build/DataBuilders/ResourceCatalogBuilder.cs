using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    internal static partial class ResourceCatalogBuilder
    {
        public static unsafe byte[] Build(ICollection<EntryDef> entries, Dictionary<GroupKey, AssetBundleId> keyToId)
        {
            L.I("[ResourceCatalogBuilder] Building ResourceCatalog...");

            // Build a sorted list of all bundle IDs => define "canonical" index
            var allBundles = keyToId.Values.ToArray();
            Array.Sort(allBundles);
            Assert.IsTrue(allBundles[0] == AssetBundleId.MonoScript, "MonoScript bundle not first in sorted list.");
            Assert.IsTrue(allBundles[1] == AssetBundleId.BuiltInShaders, "BuiltInShaders bundle not second in sorted list.");

            // Map from old AssetBundleId -> new canonical index (0..bundleCount-1)
            var idToIndex = allBundles
                .Select((id, i) => KeyValuePair.Create(id, (AssetBundleIndex) i))
                .ToDictionary();
            var keyToIndex = keyToId
                .ToDictionary(x => x.Key, x => idToIndex[x.Value]);

            // Build AssetBundle dependency data.
            var bundleKeys = keyToId.Keys;
            var deps = bundleKeys.ToDictionary(
                x => keyToIndex[x],
                x => CollectDeps(x, entries, keyToIndex));

            // Build DepSpan and DepData.
            var bundleCount = allBundles.Length;
            var (spanData, depData) = BuildDepSpan(deps);
            Assert.AreEqual(bundleCount, spanData.Length, "Invalid span data length.");

            // Gather addresses + the raw (old) bundle ID for each address
            var assetInfo = entries
                .Where(x => x.Address.HasValue)
                .Where(x => keyToId[x.Bundle].AddressAccess()) // don't write addresses for direct access bundles.
                .Select(x => new AssetInfo(x.Address.Value, keyToIndex[x.Bundle]))
                .OrderBy(x => x.Address)
                .ToList();

            L.I("[ResourceCatalogBuilder] All addresses: " + string.Join(", ",
                assetInfo.Select(x => x.Address.ReadableString())));
            Assert.AreEqual(assetInfo.Count, assetInfo.Select(x => x.Address).Distinct().Count(),
                "Duplicate address found.");

            var assetCount = assetInfo.Count;
            var addressesSize = 4 * assetCount;
            var depSpanSize = 4 * bundleCount;
            var correspondingBundlesSize = 2 * assetCount;
            var assetBundleIdsSize = 2 * bundleCount;
            var depDataSize = 2 * depData.Length;

            var data = new byte[
                2 + 2
                  + addressesSize
                  + depSpanSize
                  + correspondingBundlesSize
                  + assetBundleIdsSize
                  + depDataSize];

            // Write data in the new order
            fixed (byte* ptr = data)
            {
                var p = ptr;

                // AssetBundleCount, AssetCount
                *(ushort*) p = (ushort) bundleCount;
                p += 2;
                *(ushort*) p = (ushort) assetCount;
                p += 2;

                // Addresses (4 bytes each)
                foreach (var addressInfo in assetInfo)
                {
                    *(uint*) p = addressInfo.Address.Value();
                    p += 4;
                }

                // AssetBundleDepSpans (4 bytes each => 2 bytes Start, 2 bytes Count)
                var depOffset = 2 + 2 + addressesSize + depSpanSize + correspondingBundlesSize + assetBundleIdsSize;
                for (var i = 0; i < bundleCount; i++)
                {
                    var pointer = spanData[i].Start * 2 + depOffset;
                    *(ushort*) p = (ushort) pointer;
                    p += 2;
                    *(ushort*) p = spanData[i].Count;
                    p += 2;
                }

                // CorrespondingAssetBundles (2 bytes each) => store the canonical index of each asset's bundle
                foreach (var addressInfo in assetInfo)
                {
                    *(ushort*) p = (ushort) addressInfo.BundleIndex;
                    p += 2;
                }

                // AssetBundleIds (2 bytes each), sorted ascending
                foreach (var bundleId in allBundles)
                {
                    *(ushort*) p = bundleId.Value();
                    p += 2;
                }

                // AssetBundleDepData (2 bytes each)
                foreach (var bundleIndex in depData)
                {
                    *(ushort*) p = (ushort) bundleIndex;
                    p += 2;
                }
            }

            L.I($"[ResourceCatalogBuilder] ResourceCatalog built. {data.Length} bytes\n"
                + $"  AssetBundleCount: {bundleCount}\n"
                + $"  AssetCount: {assetCount}\n"
                + $"  Addresses: {addressesSize} bytes\n"
                + $"  AssetBundleDepSpans: {depSpanSize} bytes\n"
                + $"  CorrespondingAssetBundles: {correspondingBundlesSize} bytes\n"
                + $"  AssetBundleIds: {assetBundleIdsSize} bytes\n"
                + $"  AssetBundleDepData: {depDataSize} bytes");

            return data;
        }

        public static Dictionary<GroupKey, AssetBundleId> BuildBundleIdMap(AddressableCatalog catalog)
        {
            var groups = catalog.Groups;
            var keyToId = new Dictionary<GroupKey, AssetBundleId>(catalog.Groups.Length + 2)
            {
                { BundleNames.MonoScriptGroupKey, AssetBundleId.MonoScript },
                { BundleNames.BuiltInShadersGroupKey, AssetBundleId.BuiltInShaders }
            };
            foreach (var g in groups)
                keyToId.Add(g.Key, g.BundleId);
            return keyToId;
        }

        private readonly struct AssetInfo
        {
            public readonly Address Address;
            public readonly AssetBundleIndex BundleIndex;

            public AssetInfo(Address address, AssetBundleIndex bundleIndex)
            {
                Address = address;
                BundleIndex = bundleIndex;
            }
        }
    }
}