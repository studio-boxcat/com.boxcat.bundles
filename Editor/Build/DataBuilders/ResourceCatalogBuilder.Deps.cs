using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    internal static partial class ResourceCatalogBuilder
    {
        private static HashSet<AssetBundleIndex> CollectDeps(
            GroupKey bundle,
            ICollection<EntryDef> entries,
            Dictionary<GroupKey, AssetBundleIndex> keyToIndex)
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

            // Map to AssetBundleId.
            return deps.Select(x => keyToIndex[x]).ToHashSet();
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
            public readonly int Bundle; // AssetBundleCanonicalIndex
            public readonly int[] Deps; // AssetBundleCanonicalIndex[] (sorted)
            public DepNode Parent;
            public readonly List<DepNode> Children = new();

            public DepNode(KeyValuePair<AssetBundleIndex, HashSet<AssetBundleIndex>> bundle)
            {
                Bundle = (int) bundle.Key;
                Deps = bundle.Value
                    .OrderBy(x => x)
                    .Select(x => (int) x)
                    .ToArray();
            }
        }

        /// <summary>
        /// Returns (SpanData, DepData).
        ///   SpanData: an array of DepSpan, one per bundle
        ///   DepData: the flattened dependency IDs in the order determined by the SpanData.
        /// </summary>
        private static (DepSpan[] SpanData, AssetBundleIndex[] DepData) BuildDepSpan(
            Dictionary<AssetBundleIndex, HashSet<AssetBundleIndex>> bundleDeps)
        {
            var nodes = bundleDeps
                .Select(x => new DepNode(x))
                .ToList();

            // If an item is subset of another, set parent and remove from the list.
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
                    }
                }
            }

            // remove all nodes that have a parent.
            for (var i = nodes.Count - 1; i >= 0; i--)
            {
                if (nodes[i].Parent is not null)
                    nodes.RemoveAt(i);
            }

            // Sort root nodes by bundle ID.
            nodes.Sort((x, y) => x.Bundle.CompareTo(y.Bundle));

            var spanData = new DepSpan[bundleDeps.Count]; // index = AssetBundleCanonicalIndex
            var depData = new List<AssetBundleIndex>();

            // Flatten each root and its children.
            foreach (var node in nodes)
            {
                // Write span data.
                var depStart = depData.Count;
                spanData[node.Bundle] = new DepSpan(depStart, node.Deps.Length);

                // Write dependency data.
                depData.AddRange(node.Deps.Select(x => (AssetBundleIndex) x));

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
                        spanData[child.Bundle] = new DepSpan(depStart, 0);
                        continue;
                    }

                    var minDep = (AssetBundleIndex) child.Deps.Min();
                    var minIndex = depData.IndexOf(minDep, depStart);
                    Assert.IsTrue(minIndex >= 0, "Dependency not found in parent list.");
                    spanData[child.Bundle] = new DepSpan(minIndex, child.Deps.Length); // dep start
                }
            }

            // Validate that the reconstructed sets match the original sets.
            foreach (var (bundleId, depsSet) in bundleDeps)
            {
                var span = spanData[(int) bundleId];
                var subset = depData.Skip(span.Start).Take(span.Count).ToHashSet();
                Assert.IsTrue(depsSet.SetEquals(subset),
                    $"Invalid span data: {bundleId} (canonical index), " +
                    $"[{string.Join(", ", depsSet)}] != [{string.Join(", ", subset)}]");
            }

            return (spanData, depData.ToArray());

            static bool IsSequentialSubset(int[] parentDeps, int[] subsetDeps)
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