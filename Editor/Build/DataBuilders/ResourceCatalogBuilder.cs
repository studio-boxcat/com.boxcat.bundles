using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    internal static class ResourceCatalogBuilder
    {
        public static unsafe byte[] Build(ICollection<EntryDef> entries, Dictionary<GroupKey, AssetBundleId> keyToId)
        {
            L.I("[ResourceCatalogBuilder] Building ResourceCatalog...");

            Assert.IsTrue(keyToId.Count < byte.MaxValue, "Too many asset bundles.");

            // Build AssetBundle dependency data.
            var bundleKeys = keyToId.Keys;
            var deps = bundleKeys.ToDictionary(
                x => keyToId[x],
                x => CollectDeps(x, entries, keyToId));

            // Build AssetBundleSpan and AssetBundleDepData.
            var (spanData, depData) = BuildDepSpan(deps);
            Assert.AreEqual(bundleKeys.Count, spanData.Length, "Invalid span data length.");

            // Get all addresses.
            var addresses = entries
                .Where(x => x.Address.HasValue)
                .OrderBy(x => x.Address.Value) // Sort by address.
                .Select(x =>
                {
                    var address = (uint) x.Address.Value;
                    var bundle = x.Bundle;
                    Assert.AreEqual(0, address & 0xFF000000, "MSB 1 byte is reserved for AssetBundleId.");
                    return (uint) ((byte) keyToId[bundle] << 24) | address;
                })
                .ToList();
            L.I("[ResourceCatalogBuilder] All addresses: " + string.Join(", ", addresses.Select(x => ((Address) (x & 0x00FFFFFF)).ReadableString())));
            Assert.AreEqual(addresses.Count, addresses.Distinct().Count(), "Duplicate address found.");

            // AssetBundleCount: ushort
            // ResourceLocationCount: ushort
            // AssetBundleDepSpans: uint[]
            //     Start: ushort
            //     Count: ushort
            // ResourceLocations: uint[] (Sorted by Address)
            //     AssetBundle: 1 byte
            //     Address: 3 bytes
            // AssetBundleDepData: byte[]
            var bundleCount = bundleKeys.Count;
            var addressCount = addresses.Count;
            Assert.IsTrue(bundleCount <= byte.MaxValue, "Too many asset bundles.");
            var data = new byte[
                2 + 2
                  + bundleCount * 4
                  + addressCount * 4
                  + depData.Length];

            fixed (byte* ptr = data)
            {
                var p = ptr;
                *(ushort*) p = (ushort) bundleCount;
                p += 2;
                *(ushort*) p = (ushort) addressCount;
                p += 2;

                // Write AssetBundleDepSpans.
                var depOffset = 4 + 4 * bundleCount + 4 * addressCount;
                for (var i = 0; i < bundleCount; i++)
                {
                    var start = spanData[i].Start + depOffset;
                    Assert.IsTrue(start <= ushort.MaxValue, "Start is out of range.");
                    *(ushort*) p = (ushort) start;
                    p += 2;
                    *(ushort*) p = spanData[i].Count;
                    p += 2;
                }

                // Write ResourceLocations.
                foreach (var address in addresses)
                {
                    *(uint*) p = address;
                    p += 4;
                }

                // Write AssetBundleDepData.
                foreach (var dep in depData)
                {
                    *p = (byte) dep;
                    p += 1;
                }
            }

            L.I($"[ResourceCatalogBuilder] ResourceCatalog built. {data.Length} bytes\n"
                + $"  AssetBundleCount: {bundleCount}\n"
                + $"  ResourceLocationCount: {addressCount}\n"
                + $"  AssetBundleDepSpans: {bundleCount * 4} bytes\n"
                + $"  ResourceLocations: {addressCount * 4} bytes\n"
                + $"  AssetBundleDepData: {depData.Length} bytes");
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
            foreach (var g in groups) keyToId.Add(g.Key, g.BundleId);
            return keyToId;
        }

        private static HashSet<AssetBundleId> CollectDeps(GroupKey bundle, ICollection<EntryDef> entries, Dictionary<GroupKey, AssetBundleId> keyToId)
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

            // Remove MonoScript bundle from deps as it will be loaded manually. See AssetBundleLoader.cs.
            deps.Remove((GroupKey) BundleNames.MonoScript);

            L.I($"[ResourceCatalogBuilder] Dependencies of {bundle.Value}: {string.Join(", ", deps.Select(x => x.Value))}");

            // Build final dependency data.
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

        private static (DepSpan[] SpanData, AssetBundleId[] DepData) BuildDepSpan(Dictionary<AssetBundleId, HashSet<AssetBundleId>> bundleDeps)
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

            // Sort root nodes by bundle.
            nodes.Sort((x, y) => x.Bundle.CompareTo(y.Bundle));

            // Write span data.
            var spanData = new DepSpan[bundleDeps.Count];
            var depData = new List<AssetBundleId>();
            foreach (var node in nodes) // Root nodes.
            {
                // Write span data.
                var depStart = depData.Count;
                spanData[(int) node.Bundle] = new DepSpan(depStart, node.Deps.Length);

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
                children.Sort((x, y) => x.Bundle.CompareTo(y.Bundle));

                // Write span data for children.
                foreach (var child in children)
                {
                    if (child.Deps.Length == 0)
                    {
                        spanData[(int) child.Bundle] = new DepSpan(depStart, 0); // dep start
                        continue;
                    }

                    var min = child.Deps.Min();
                    var minIndex = depData.IndexOf(min, depStart);
                    Assert.IsTrue(minIndex >= 0, "Dependency not found.");
                    spanData[(int) child.Bundle] = new DepSpan(minIndex, child.Deps.Length); // dep start
                }
            }

            // Validate span data
            foreach (var (bundleId, deps) in bundleDeps)
            {
                var span = spanData[(int) bundleId];
                var resultDeps = depData.Skip(span.Start).Take(span.Count).ToHashSet();
                Assert.IsTrue(deps.SetEquals(resultDeps), $"Invalid span data: {bundleId.Name()}, [{string.Join(", ", deps)}] != [{string.Join(", ", resultDeps)}]");
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
    }
}