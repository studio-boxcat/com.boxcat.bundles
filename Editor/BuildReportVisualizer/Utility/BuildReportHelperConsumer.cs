#if UNITY_2022_2_OR_NEWER
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build.Layout;

namespace UnityEditor.AddressableAssets.BuildReportVisualizer
{
    internal abstract class BuildReportHelperAsset
    {
        public abstract BuildLayout.ExplicitAsset ImmediateReferencingAsset { get; set; }
        public abstract SortedDictionary<AssetId, BuildLayout.ExplicitAsset> GUIDToReferencingAssets { get; set; }
    }

    internal class BuildReportHelperExplicitAsset : BuildReportHelperAsset
    {
        public override BuildLayout.ExplicitAsset ImmediateReferencingAsset { get; set; }
        public override SortedDictionary<AssetId, BuildLayout.ExplicitAsset> GUIDToReferencingAssets { get; set; }

        public BuildLayout.ExplicitAsset Asset;
        public SortedDictionary<AssetId, BuildReportHelperExplicitAssetDependency> GUIDToInternalReferencedExplicitAssets;
        public SortedDictionary<AssetId, BuildReportHelperImplicitAssetDependency> GUIDToInternalReferencedOtherAssets;
        public SortedDictionary<AssetId, BuildReportHelperAssetDependency> GUIDToExternallyReferencedAssets;

        public BuildReportHelperExplicitAsset(BuildLayout.ExplicitAsset asset, BuildLayout.ExplicitAsset referencingAsset, SortedDictionary<AssetId, BuildReportHelperDuplicateImplicitAsset> duplicateAssets)
        {
            Asset = asset;
            ImmediateReferencingAsset = referencingAsset;

            GUIDToInternalReferencedExplicitAssets = new SortedDictionary<AssetId, BuildReportHelperExplicitAssetDependency>();
            GUIDToInternalReferencedOtherAssets = new SortedDictionary<AssetId, BuildReportHelperImplicitAssetDependency>();
            GUIDToExternallyReferencedAssets = new SortedDictionary<AssetId, BuildReportHelperAssetDependency>();

            GenerateFlatListOfReferencedAssets(Asset, Asset, GUIDToInternalReferencedExplicitAssets, GUIDToInternalReferencedOtherAssets, GUIDToExternallyReferencedAssets, duplicateAssets);
            GUIDToReferencingAssets = BuildReportUtility.GetReferencingAssets(Asset);
        }

        private void GenerateFlatListOfReferencedAssets(BuildLayout.ExplicitAsset asset, BuildLayout.ExplicitAsset mainAsset,
            SortedDictionary<AssetId, BuildReportHelperExplicitAssetDependency> internalReferencedExplicitAssets,
            SortedDictionary<AssetId, BuildReportHelperImplicitAssetDependency> internalReferencedOtherAssets,
            SortedDictionary<AssetId, BuildReportHelperAssetDependency> externallyReferencedAssets,
            SortedDictionary<AssetId, BuildReportHelperDuplicateImplicitAsset> duplicateAssets)
        {
            foreach (var explicitDep in asset.InternalReferencedExplicitAssets)
            {
                if (asset.Bundle == mainAsset.Bundle && !internalReferencedExplicitAssets.ContainsKey(explicitDep.Guid))
                    internalReferencedExplicitAssets.TryAdd(explicitDep.Guid, new BuildReportHelperExplicitAssetDependency(explicitDep, asset));
                else if (asset.Bundle != mainAsset.Bundle && !externallyReferencedAssets.ContainsKey(explicitDep.Guid))
                    externallyReferencedAssets.TryAdd(explicitDep.Guid, new BuildReportHelperExplicitAssetDependency(explicitDep, asset));
                GenerateFlatListOfReferencedAssets(explicitDep, mainAsset, internalReferencedExplicitAssets, internalReferencedOtherAssets, externallyReferencedAssets, duplicateAssets);
            }

            foreach (var implicitDep in asset.InternalReferencedOtherAssets)
            {
                if (asset.Bundle == mainAsset.Bundle && !internalReferencedOtherAssets.ContainsKey(implicitDep.AssetGuid))
                {
                    var dep = duplicateAssets.TryGetValue(implicitDep.AssetGuid, out var duplicateAsset)
                        ? new BuildReportHelperImplicitAssetDependency(duplicateAsset, asset)
                        : new BuildReportHelperImplicitAssetDependency(implicitDep, asset);
                    internalReferencedOtherAssets.TryAdd(implicitDep.AssetGuid, dep);
                }
                else if (asset.Bundle != mainAsset.Bundle && !externallyReferencedAssets.ContainsKey(implicitDep.AssetGuid))
                {
                    var dep = duplicateAssets.TryGetValue(implicitDep.AssetGuid, out var duplicateAsset)
                        ? new BuildReportHelperImplicitAssetDependency(duplicateAsset, asset)
                        : new BuildReportHelperImplicitAssetDependency(implicitDep, asset);
                    externallyReferencedAssets.TryAdd(implicitDep.AssetGuid, dep);
                }
            }

            foreach (BuildLayout.ExplicitAsset explicitDep in asset.ExternallyReferencedAssets)
            {
                if (!externallyReferencedAssets.ContainsKey(explicitDep.Guid))
                    externallyReferencedAssets.TryAdd(explicitDep.Guid, new BuildReportHelperExplicitAssetDependency(explicitDep, asset));
                GenerateFlatListOfReferencedAssets(explicitDep, mainAsset, internalReferencedExplicitAssets, internalReferencedOtherAssets, externallyReferencedAssets, duplicateAssets);
            }
        }
    }

    internal abstract class BuildReportHelperAssetDependency
    {
        public abstract BuildLayout.ExplicitAsset ImmediateReferencingAsset { get; set; }
        public abstract SortedDictionary<AssetId, BuildLayout.ExplicitAsset> GUIDToReferencingAssets { get; set; }
    }

    internal class BuildReportHelperExplicitAssetDependency : BuildReportHelperAssetDependency
    {
        public BuildLayout.ExplicitAsset Asset { get; set; }
        public override BuildLayout.ExplicitAsset ImmediateReferencingAsset { get; set; }
        public override SortedDictionary<AssetId, BuildLayout.ExplicitAsset> GUIDToReferencingAssets { get; set; }

        public BuildReportHelperExplicitAssetDependency(BuildLayout.ExplicitAsset asset, BuildLayout.ExplicitAsset referencingAsset)
        {
            Asset = asset;
            ImmediateReferencingAsset = referencingAsset;
            GUIDToReferencingAssets = BuildReportUtility.GetReferencingAssets(Asset);
        }
    }

    internal class BuildReportHelperImplicitAssetDependency : BuildReportHelperAssetDependency
    {
        public BuildLayout.DataFromOtherAsset Asset { get; set; }
        public override BuildLayout.ExplicitAsset ImmediateReferencingAsset { get; set; }
        public override SortedDictionary<AssetId, BuildLayout.ExplicitAsset> GUIDToReferencingAssets { get; set; }

        public List<BuildLayout.Bundle> Bundles { get; set; }

        public BuildReportHelperImplicitAssetDependency(BuildLayout.DataFromOtherAsset asset, BuildLayout.ExplicitAsset immediateReferencingAsset)
        {
            Asset = asset;
            ImmediateReferencingAsset = immediateReferencingAsset;
            Bundles = new List<BuildLayout.Bundle>() {asset.File.Bundle};
            GUIDToReferencingAssets = new SortedDictionary<AssetId, BuildLayout.ExplicitAsset>();
            foreach (BuildLayout.ExplicitAsset referencingAsset in asset.ReferencingAssets)
            {
                GUIDToReferencingAssets.TryAdd(referencingAsset.Guid, referencingAsset);
            }
        }

        public BuildReportHelperImplicitAssetDependency(BuildReportHelperDuplicateImplicitAsset duplicateAsset, BuildLayout.ExplicitAsset immediateReferencingAsset)
        {
            Asset = duplicateAsset.Asset;
            ImmediateReferencingAsset = immediateReferencingAsset;
            Bundles = duplicateAsset.Bundles;
            GUIDToReferencingAssets = duplicateAsset.GUIDToReferencingAssets;
        }
    }

    internal class BuildReportHelperDuplicateImplicitAsset : BuildReportHelperAsset
    {
        public override BuildLayout.ExplicitAsset ImmediateReferencingAsset { get; set; }

        public List<BuildLayout.Bundle> Bundles { get; set; }

        public BuildLayout.DataFromOtherAsset Asset;

        public override SortedDictionary<AssetId, BuildLayout.ExplicitAsset> GUIDToReferencingAssets { get; set; }

        public BuildReportHelperDuplicateImplicitAsset(BuildLayout.DataFromOtherAsset asset, BuildLayout.AssetDuplicationData assetDupData)
        {
            Asset = asset;
            Bundles = new List<BuildLayout.Bundle>();
            GUIDToReferencingAssets = new SortedDictionary<AssetId, BuildLayout.ExplicitAsset>();

            foreach (BuildLayout.File bundleFile in assetDupData.DuplicatedObjects.SelectMany(o => o.IncludedInBundleFiles))
            {
                Bundles.Add(bundleFile.Bundle);
                foreach (BuildLayout.ExplicitAsset explicitAsset in bundleFile.Assets)
                {
                    foreach (BuildLayout.DataFromOtherAsset otherAsset in explicitAsset.InternalReferencedOtherAssets)
                    {
                        if (otherAsset.AssetGuid == asset.AssetGuid)
                        {
                            GUIDToReferencingAssets.TryAdd(explicitAsset.Guid, explicitAsset);
                        }
                    }
                }
            }
        }
    }

    internal class BuildReportHelperConsumer : IBuildReportConsumer
    {
        private SortedDictionary<AssetId, BuildLayout.DataFromOtherAsset> m_GUIDToImplicitAssets;
        private SortedDictionary<AssetId, BuildReportHelperDuplicateImplicitAsset> m_GUIDToDuplicateAssets;

        internal SortedDictionary<AssetId, BuildReportHelperDuplicateImplicitAsset> GUIDToDuplicateAssets => m_GUIDToDuplicateAssets;

        public void Consume(BuildLayout buildReport)
        {
            m_GUIDToImplicitAssets = GetGUIDToImplicitAssets(buildReport);
            m_GUIDToDuplicateAssets = GetGUIDToDuplicateAssets(buildReport, m_GUIDToImplicitAssets);
        }

        private static SortedDictionary<AssetId, BuildLayout.DataFromOtherAsset> GetGUIDToImplicitAssets(BuildLayout report)
        {
            var guidToImplicitAssets = new SortedDictionary<AssetId, BuildLayout.DataFromOtherAsset>();
            var allInstancesOfImplicitAssets = BuildLayoutHelpers.EnumerateBundles(report).SelectMany(b => b.Files).SelectMany(f => f.Assets).SelectMany(a => a.InternalReferencedOtherAssets);

            foreach (BuildLayout.DataFromOtherAsset asset in allInstancesOfImplicitAssets)
            {
                if (!guidToImplicitAssets.ContainsKey(asset.AssetGuid))
                {
                    guidToImplicitAssets.TryAdd(asset.AssetGuid, asset);
                }
            }
            return guidToImplicitAssets;
        }

        private static SortedDictionary<AssetId, BuildReportHelperDuplicateImplicitAsset> GetGUIDToDuplicateAssets(
            BuildLayout report, SortedDictionary<AssetId, BuildLayout.DataFromOtherAsset> guidToImplicitAssets)
        {
            var duplicateAssets = new SortedDictionary<AssetId, BuildReportHelperDuplicateImplicitAsset>();
            foreach (BuildLayout.AssetDuplicationData dupData in report.DuplicatedAssets)
            {
                var helperDupAsset = new BuildReportHelperDuplicateImplicitAsset(guidToImplicitAssets[dupData.AssetGuid], dupData);
                duplicateAssets.TryAdd(dupData.AssetGuid, helperDupAsset);
            }
            return duplicateAssets;
        }
    }
}
#endif