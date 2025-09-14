using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.Assertions;

namespace Bundles.Editor
{
    internal static partial class ResourceCatalogBuilder
    {
        public static void Build(BundlesBuildContext ctx, string outputPath)
        {
            var bundles = ctx.entries.Values.SelectMany(x => x.Dependencies)
                .ToHashSet().OrderBy(x => x).ToArray();

            // Map from AssetBundleId -> AssetBundleIndex
            var idToIndex = bundles
                .Select((id, i) => KeyValuePair.Create(id, (AssetBundleIndex) i))
                .ToDictionary();

            // Build AssetInfo data.
            var assetInfo = BuildAssetInfos(ctx.entries, idToIndex);

            // Build AssetBundle dependency data.
            var deps = bundles.ToDictionary(
                x => idToIndex[x],
                x => CollectDeps(x, ctx.entries.Values, idToIndex));

            // Write the catalog to disk.
            var bytes = DoBuild(bundles, assetInfo, deps);
            File.WriteAllBytes(outputPath, bytes); // if this file exists, overwrite it

            // Store dependency data in the catalog.
            var indexToName = bundles
                .Select((x, i) => KeyValuePair.Create((AssetBundleIndex) i, ctx.Catalog.ResolveGroupKeyForDisplay(x).Value))
                .ToDictionary();
            foreach (var (bundleIndex, bundleDeps) in deps)
            {
                var id = bundles[(int) bundleIndex];
                if (id is AssetBundleId.MonoScript) continue;
                var group = ctx.Catalog.GetGroup(id);
                group.LastDependency = string.Join(", ", bundleDeps.Select(x => indexToName[x]));
                L.I($"[ResourceCatalogBuilder] Dependencies of {indexToName[bundleIndex]}: {group.LastDependency}");
            }
            EditorUtility.SetDirty(ctx.Catalog);
        }

        private static AssetInfo[] BuildAssetInfos(Dictionary<GUID, EntryDef> entries, Dictionary<AssetBundleId, AssetBundleIndex> idToIndex)
        {
            // Gather addresses + the raw (old) bundle ID for each address
            return entries.Values
                .Where(x => x.Address.HasValue)
                .Where(x => x.Bundle.AddressAccess()) // don't write addresses for direct access bundles.
                .Select(x => new AssetInfo(x.Address.Value, idToIndex[x.Bundle]))
                .OrderBy(x => x.Address)
                .ToArray();
        }

        private static unsafe byte[] DoBuild(AssetBundleId[] bundles, AssetInfo[] assets, Dictionary<AssetBundleIndex, AssetBundleIndex[]> deps)
        {
            L.I("[ResourceCatalogBuilder] Building ResourceCatalog...");

            // Build a sorted list of all bundle IDs => define "canonical" index
            Array.Sort(bundles);
            Assert.IsTrue(bundles[0] == AssetBundleId.MonoScript, "MonoScript bundle not first in sorted list.");

            // Build DepSpan and DepData.
            var bundleCount = bundles.Length;
            var (spanData, depData) = BuildDepSpan(deps);
            Assert.AreEqual(bundleCount, spanData.Length, "Invalid span data length.");

            L.I("[ResourceCatalogBuilder] All addresses: " + string.Join(", ",
                assets.Select(x => x.Address.Name())));
            Assert.AreEqual(assets.Length, assets.Select(x => x.Address).Distinct().Count(),
                "Duplicate address found.");

            var assetCount = assets.Length;
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
                foreach (var addressInfo in assets)
                {
                    *(uint*) p = addressInfo.Address.Val();
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
                foreach (var addressInfo in assets)
                {
                    *(ushort*) p = (ushort) addressInfo.BundleIndex;
                    p += 2;
                }

                // AssetBundleIds (2 bytes each), sorted ascending
                foreach (var bundleId in bundles)
                {
                    *(ushort*) p = bundleId.Val();
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