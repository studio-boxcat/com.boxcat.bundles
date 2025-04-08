using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    internal static class ResourceCatalogBuilder
    {
        public static unsafe byte[] Build(ICollection<EntryDef> entries, Dictionary<GroupKey, AssetBundleId> keyToId)
        {
            L.I("[ResourceCatalogBuilder] Building ResourceCatalog...");

            Assert.IsTrue(keyToId.Count <= ushort.MaxValue, "Too many asset bundles.");

            // Build AssetBundle dependency data.
            var bundleKeys = keyToId.Keys;
            var deps = bundleKeys.ToDictionary(
                x => keyToId[x],
                x => CollectDeps(x, entries, keyToId));

            // Build DepSpan and DepData.
            var (spanData, depData) = BuildDepSpan(deps);
            Assert.AreEqual(bundleKeys.Count, spanData.Length, "Invalid span data length.");

            // Collect addresses separately from their bundle IDs (instead of packing them).
            // We'll store addresses in 32 bits each, sorted ascending,
            // and store the corresponding AssetBundleId in 16 bits each.
            var assetInfo = entries
                .Where(x => x.Address.HasValue)
                .Select(x => new AssetInfo(x.Address.Value, keyToId[x.Bundle]))
                .OrderBy(x => x.Address)
                .ToList();

            L.I("[ResourceCatalogBuilder] All addresses: " + string.Join(", ",
                assetInfo.Select(x => x.Address.ReadableString())));
            Assert.AreEqual(assetInfo.Count, assetInfo.Select(x => x.Address).Distinct().Count(),
                "Duplicate address found.");


            // AssetBundleCount: ushort
            // AssetCount: ushort
            // AssetBundleDepSpans: uint[]
            //     Start: ushort
            //     Count: ushort
            // Addresses: uint[] (Sorted)
            // CorrespondingAssetBundles: ushort[] - AssetBundleIds
            // OptionalPadding: 0 or 2 bytes
            // AssetBundleDepData: ushort[] - AssetBundleIds
            var bundleCount = bundleKeys.Count;
            var assetCount = assetInfo.Count;
            var depSpanSize = 4 * bundleCount;
            var addressesSize = 4 * assetCount; // each address is 4 bytes
            var bundleIdsSize = 2 * assetCount; // each bundle ID is 2 bytes
            // Simple alignment: if we've written an odd multiple of 2 bytes, add 2 bytes padding so DepData begins on a 4-byte boundary.
            var optionalPadding = (assetCount & 1) == 1 ? 2 : 0;
            var depDataSize = 2 * depData.Length; // each dep is now 2 bytes

            Assert.IsTrue(bundleCount <= byte.MaxValue, "Too many asset bundles.");
            var data = new byte[
                2 + 2 // bundleCount, resourceLocationCount
                  + depSpanSize // AssetBundleDepSpans
                  + addressesSize
                  + bundleIdsSize
                  + optionalPadding
                  + depDataSize];

            fixed (byte* ptr = data)
            {
                var p = ptr;

                // Write AssetBundleCount, ResourceLocationCount.
                *(ushort*) p = (ushort) bundleCount;
                p += 2;
                *(ushort*) p = (ushort) assetCount;
                p += 2;

                // Write AssetBundleDepSpans.
                // We'll need to compute the actual start of DepData for each bundle's DepSpan.
                // DepData begins after all the preceding sections:
                //     2 + 2 (counts) + depSpanSize + addressesSize + bundleIdsSize + optionalPadding
                var depOffset = 2 + 2 + depSpanSize + addressesSize + bundleIdsSize + optionalPadding;
                for (var i = 0; i < bundleCount; i++)
                {
                    // Each DepSpan uses 'Start' as an index (count of AssetBundleIds), so we multiply by 2 bytes per dep ID
                    // then add depDataStart to get an absolute offset in bytes.
                    var start = spanData[i].Start * 2 + depOffset;
                    Assert.IsTrue(start <= ushort.MaxValue, "Start is out of range.");
                    *(ushort*) p = (ushort) start;
                    p += 2;
                    *(ushort*) p = spanData[i].Count;
                    p += 2;
                }

                // Write Addresses (4 bytes each).
                foreach (var addressInfo in assetInfo)
                {
                    *(uint*) p = addressInfo.Address.Value();
                    p += 4;
                }

                // Write Bundle IDs (2 bytes each).
                foreach (var addressInfo in assetInfo)
                {
                    *(ushort*) p = addressInfo.BundleId.Value();
                    p += 2;
                }

                // Optional 2-byte padding for alignment.
                if (optionalPadding == 2)
                {
                    *(ushort*) p = 0;
                    p += 2;
                }

                // Write DepData (2 bytes for each dependency ID).
                foreach (var dep in depData)
                {
                    *(ushort*) p = dep.Value();
                    p += 2;
                }
            }

            L.I($"[ResourceCatalogBuilder] ResourceCatalog built. {data.Length} bytes\n"
                + $"  AssetBundleCount: {bundleCount}\n"
                + $"  AssetCount: {assetCount}\n"
                + $"  AssetBundleDepSpans: {depSpanSize} bytes\n"
                + $"  Addresses: {addressesSize} bytes\n"
                + $"  CorrespondingAssetBundles: {bundleIdsSize} bytes\n"
                + $"  OptionalPadding: {optionalPadding} bytes\n"
                + $"  AssetBundleDepData: {depDataSize} bytes");

            return data;
        }

        public static Dictionary<GroupKey, AssetBundleId> BuildBundleIdMap(AddressableCatalog catalog)
        {
            var groups = catalog.Groups;
            // 2 for MonoScriptBundle and BuiltInShaderBundle.
            var keyToId = new Dictionary<GroupKey, AssetBundleId>(catalog.Groups.Length + 2)
            {
                { (GroupKey) BundleNames.MonoScript, AssetBundleId.MonoScript },
                { (GroupKey) BundleNames.BuiltInShaders, AssetBundleId.BuiltInShader }
            };
            foreach (var g in groups)
                keyToId.Add(g.Key, g.BundleId);
            return keyToId;
        }

        private static HashSet<AssetBundleId> CollectDeps(
            GroupKey bundle,
            ICollection<EntryDef> entries,
            Dictionary<GroupKey, AssetBundleId> keyToId)
        {
            var deps = new HashSet<GroupKey>();

            // Collect all dependencies of the bundle.
            foreach (var entry in entries)
            {
                if (entry.Bundle == bundle)
                    deps.UnionWith(entry.Dependencies);
            }

            // Remove the bundle itself from the dependencies.
            deps.Remove(bundle);

            // Remove MonoScript bundle from deps as it will be loaded manually.
            deps.Remove((GroupKey) BundleNames.MonoScript);

            L.I($"[ResourceCatalogBuilder] Dependencies of {bundle.Value}: {string.Join(", ", deps.Select(x => x.Value))}");

            // Build final dependency data (mapping to AssetBundleId).
            return deps.Select(x => keyToId[x]).ToHashSet();
        }

        private readonly struct DepSpan
        {
            public readonly ushort Start;
            public readonly ushort Count;

            public DepSpan(int start, int count)
            {
                Assert.IsTrue(start <= ushort.MaxValue, "Start is out of range.");
                Assert.IsTrue(count <= ushort.MaxValue, "Count is out of range.");
                Start = (ushort) start;
                Count = (ushort) count;
            }
        }

        private class DepNode
        {
            public AssetBundleId Bundle;
            public AssetBundleId[] Deps;
            public DepNode Parent;
            public readonly List<DepNode> Children = new();
            public override string ToString() => Bundle.Name();
        }

        /// <summary>
        /// Returns (SpanData, DepData).
        ///   SpanData: an array of DepSpan, one per bundle
        ///   DepData: the flattened dependency IDs in the order determined by the SpanData.
        /// </summary>
        private static (DepSpan[] SpanData, AssetBundleId[] DepData) BuildDepSpan(
            Dictionary<AssetBundleId, HashSet<AssetBundleId>> bundleDeps)
        {
            var nodes = bundleDeps
                .Select(x => new DepNode
                {
                    Bundle = x.Key,
                    Deps = x.Value.OrderBy(y => y).ToArray()
                })
                .ToList();

            // If an item is subset of another, set parent and remove from the list.
            var toRemove = new List<DepNode>();
            for (var i = 0; i < nodes.Count; i++) // To be parent
            {
                var parent = nodes[i];
                if (parent.Parent is not null) continue; // Skip if already has a parent.

                for (var j = 0; j < nodes.Count; j++) // To be child
                {
                    if (i == j) continue;
                    var child = nodes[j];
                    if (child.Parent is not null) continue; // Skip if already has a parent.

                    if (IsSequentialSubset(parent.Deps, child.Deps))
                    {
                        child.Parent = parent;
                        parent.Children.Add(child);
                        toRemove.Add(child);
                    }
                }
            }

            foreach (var item in toRemove)
                nodes.Remove(item);

            // Sort root nodes by bundle ID.
            nodes.Sort((x, y) => x.Bundle.CompareToFast(y.Bundle));

            var spanData = new DepSpan[bundleDeps.Count]; // index = AssetBundleId
            var depData = new List<AssetBundleId>();

            // Flatten each root and its children.
            foreach (var node in nodes)
            {
                // Write span data.
                var depStart = depData.Count;
                spanData[node.Bundle.Value()] = new DepSpan(depStart, node.Deps.Length);

                // Write dependency data.
                depData.AddRange(node.Deps);

                // Flatten children.
                var toVisit = new List<DepNode>(node.Children);
                var children = new List<DepNode>();
                while (toVisit.Count > 0)
                {
                    var index = toVisit.Count - 1;
                    var current = toVisit[index];
                    toVisit.RemoveAt(index);
                    children.Add(current);
                    toVisit.AddRange(current.Children);
                }

                // Sort children by bundle.
                children.Sort((x, y) => x.Bundle.CompareToFast(y.Bundle));

                // Write span data for children.
                foreach (var child in children)
                {
                    if (child.Deps.Length == 0)
                    {
                        spanData[child.Bundle.Value()] = new DepSpan(depStart, 0);
                        continue;
                    }

                    var minDep = child.Deps.Min();
                    var minIndex = depData.IndexOf(minDep, depStart);
                    Assert.IsTrue(minIndex >= 0, "Dependency not found in parent list.");
                    spanData[child.Bundle.Value()] = new DepSpan(minIndex, child.Deps.Length); // dep start
                }
            }

            // Validate that the reconstructed sets match the original sets.
            foreach (var (bundleId, depsSet) in bundleDeps)
            {
                var span = spanData[(int) bundleId];
                var subset = depData.Skip(span.Start).Take(span.Count).ToHashSet();
                Assert.IsTrue(depsSet.SetEquals(subset),
                    $"Invalid span data: {bundleId.Name()}, " +
                    $"[{string.Join(", ", depsSet)}] != [{string.Join(", ", subset)}]");
            }

            return (spanData, depData.ToArray());

            static bool IsSequentialSubset(AssetBundleId[] parentDeps, AssetBundleId[] subsetDeps)
            {
                // Trivial case
                if (subsetDeps.Length is 0)
                    return true;
                if (parentDeps.Length < subsetDeps.Length)
                    return false;

                // Find start index
                var startIndex = Array.IndexOf(parentDeps, subsetDeps[0]);
                if (startIndex is -1)
                    return false;

                // Out of range
                if (startIndex + subsetDeps.Length > parentDeps.Length)
                    return false;

                // Check if the rest is the same
                for (var i = 1; i < subsetDeps.Length; i++)
                {
                    if (parentDeps[startIndex + i] != subsetDeps[i])
                        return false;
                }

                return true;
            }
        }

        private readonly struct AssetInfo
        {
            public readonly Address Address;
            public readonly AssetBundleId BundleId;

            public AssetInfo(Address address, AssetBundleId bundleId)
            {
                Address = address;
                BundleId = bundleId;
            }
        }
    }
}