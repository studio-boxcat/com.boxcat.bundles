using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;

namespace Bundles.Editor
{
    /// <summary>
    /// The BuildTask used to create location lists for Bundles assets.
    /// </summary>
    public class GenerateLocationListsTask : IBuildTask
    {
        /// <summary>
        /// The GenerateLocationListsTask version.
        /// </summary>
        public int Version => 1;

#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        private BundlesBuildContext m_AaBuildContext;

        [InjectContext]
        private IBundleWriteData m_WriteData;
#pragma warning restore 649

        /// <summary>
        /// Runs the build task with the injected context.
        /// </summary>
        /// <returns>The success or failure ReturnCode</returns>
        public ReturnCode Run()
        {
            // Build AssetGUID -> BundleKey dictionary.
            var assetToFiles = m_WriteData.AssetToFiles.ToDictionary(
                x => x.Key,
                x => x.Value.Select(y => AssetBundleIdUtils.Parse(m_WriteData.FileToBundle[y])).ToList());

            var ctx = m_AaBuildContext;
            Process(
                ctx.Catalog,
                assetToFiles,
                out ctx.entries,
                out ctx.bundleToImmediateBundleDependencies,
                out ctx.bundleToExpandedBundleDependencies);
            return ReturnCode.Success;
        }

        /// <summary>
        /// Processes the Input data from the build and returns an organized struct of information, including dependencies and catalog loctions.
        /// </summary>
        /// <returns>An object that contains organized information about dependencies and catalog locations.</returns>
        private static void Process(
            AssetCatalog catalog,
            Dictionary<GUID, List<AssetBundleId>> assetToFiles,
            out Dictionary<GUID, EntryDef> entries,
            out Dictionary<AssetBundleId, HashSet<AssetBundleId>> bundleToImmediateBundleDependencies,
            out Dictionary<AssetBundleId, HashSet<AssetBundleId>> bundleToExpandedBundleDependencies)
        {
            entries = catalog.Groups
                .SelectMany(g =>
                {
                    return g.Entries.Select(e =>
                    {
                        var guid = e.GUID;
                        var deps = assetToFiles[guid];
                        var bundle = deps[0]; // First bundle is the containing bundle.
                        var address = AssetGroup.ResolveAddressNumeric(g, e);
                        return new EntryDef(guid, address, bundle, deps.ToHashSet());
                    });
                })
                .ToDictionary(e => e.GUID, e => e);

            // Build bundle deps.
            bundleToImmediateBundleDependencies = entries.Values
                // Construct depender to dependee mapping
                .SelectMany(x => x.Dependencies.Select(y => new { x.Bundle, y }))
                // Group by depender
                .GroupBy(x => x.Bundle)
                // Convert to HashSet
                .ToDictionary(g => g.Key, g => g.Select(x => x.y).ToHashSet());

            // Add builtin bundles.
            bundleToImmediateBundleDependencies.Add(AssetBundleId.MonoScript, new HashSet<AssetBundleId> { AssetBundleId.MonoScript });

            // Expand bundle deps.
            bundleToExpandedBundleDependencies = new Dictionary<AssetBundleId, HashSet<AssetBundleId>>();
            foreach (var bundle in bundleToImmediateBundleDependencies.Keys)
            {
                var visited = new HashSet<AssetBundleId>();
                var stack = new Stack<AssetBundleId>();
                stack.Push(bundle);
                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    if (visited.Add(current) is false) continue;
                    foreach (var dep in bundleToImmediateBundleDependencies[current])
                        stack.Push(dep);
                }
                bundleToExpandedBundleDependencies.Add(bundle, visited);
            }
        }
    }
}