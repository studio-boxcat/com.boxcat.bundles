using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets.Build.BuildPipelineTasks
{
    /// <summary>
    /// The BuildTask used to create location lists for Addressable assets.
    /// </summary>
    public class GenerateLocationListsTask : IBuildTask
    {
        /// <summary>
        /// The GenerateLocationListsTask version.
        /// </summary>
        public int Version => 1;

#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        IAddressableAssetsBuildContext m_AaBuildContext;

        [InjectContext]
        IBundleWriteData m_WriteData;
#pragma warning restore 649

        /// <summary>
        /// Runs the build task with the injected context.
        /// </summary>
        /// <returns>The success or failure ReturnCode</returns>
        public ReturnCode Run()
        {
            // Build AssetGUID -> BundleKey dictionary.
            var assetToFiles = m_WriteData.AssetToFiles.ToDictionary(
                x => (AssetGUID) x.Key,
                x => x.Value.Select(y => BundleKey.FromBuildName(m_WriteData.FileToBundle[y])).ToList());

            var ctx = (AddressableAssetsBuildContext) m_AaBuildContext;
            Process(
                ctx.Settings,
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
        static void Process(
            AddressableAssetSettings settings,
            Dictionary<AssetGUID, List<BundleKey>> assetToFiles,
            out Dictionary<AssetGUID, EntryDef> entries,
            out Dictionary<BundleKey, HashSet<BundleKey>> bundleToImmediateBundleDependencies,
            out Dictionary<BundleKey, HashSet<BundleKey>> bundleToExpandedBundleDependencies)
        {
            entries = settings.groups.SelectMany(g => g.entries).ToDictionary(
                e => e.guid,
                e =>
                {
                    var guid = e.guid;
                    var deps = assetToFiles[guid];
                    var bundle = deps[0]; // First bundle is the containing bundle.
                    return new EntryDef(guid, AddressUtils.Hash(e.address), bundle, deps.ToHashSet());
                });

            // Build bundle deps.
            bundleToImmediateBundleDependencies = entries.Values
                // Construct depender to dependee mapping
                .SelectMany(x => x.Dependencies.Select(y => new {x.Bundle, y}))
                // Group by depender
                .GroupBy(x => x.Bundle)
                // Convert to HashSet
                .ToDictionary(g => g.Key, g => g.Select(x => x.y).ToHashSet());

            // Add builtin bundles.
            bundleToImmediateBundleDependencies.Add(
                BundleKey.FromBuildName(BuildUtility.BuiltInShaderBundle),
                new HashSet<BundleKey> {BundleKey.FromBuildName(BuildUtility.BuiltInShaderBundle)});

            // Expand bundle deps.
            bundleToExpandedBundleDependencies = new Dictionary<BundleKey, HashSet<BundleKey>>();
            foreach (var bundle in bundleToImmediateBundleDependencies.Keys)
            {
                var visited = new HashSet<BundleKey>();
                var stack = new Stack<BundleKey>();
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