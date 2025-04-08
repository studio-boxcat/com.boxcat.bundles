using System.Collections.Generic;

namespace UnityEditor.AddressableAssets.Build.Layout
{
    /// <summary>
    /// Data store for summary data about build content
    /// </summary>
    public struct AssetSummary
    {
        /// <summary>
        /// Number of Objects build of the defined AssetType
        /// </summary>
        public int Count;

        /// <summary>
        /// Total size of combined Objects
        /// </summary>
        public ulong SizeInBytes;
    }

    /// <summary>
    /// Data store for summary data about Bundle content
    /// </summary>
    public struct BundleSummary
    {
        /// <summary>
        /// Number of bundles built
        /// </summary>
        public int Count;

        /// <summary>
        /// Size in bytes of bundled compressed
        /// </summary>
        public ulong TotalCompressedSize;
    }

    /// <summary>
    /// Data store for Addressables build
    /// </summary>
    internal class BuildLayoutSummary
    {
        /// <summary>
        /// Summary of bundles
        /// </summary>
        public BundleSummary BundleSummary = new BundleSummary();

        /// <summary>
        /// Summary for AssetTypes used
        /// </summary>
        public List<AssetSummary> AssetSummaries = new List<AssetSummary>();

        /// <summary>
        /// The total number of assets in a build, including implicit assets
        /// </summary>
        internal int TotalAssetCount = 0;

        /// <summary>
        /// The total number of explicitly added Addressable assets that were included in a build
        /// </summary>
        internal int ExplicitAssetCount = 0;

        /// <summary>
        /// The total number of implicitly added assets that were included in a build
        /// </summary>
        internal int ImplicitAssetCount = 0;

        /// <summary>
        /// Generates a summary of the content used in a BuildLayout, minus the asset type data.
        /// </summary>
        /// <param name="layout"></param>
        /// <returns></returns>

        internal static BuildLayoutSummary GetSummaryWithoutAssetTypes(BuildLayout layout)
        {
            BuildLayoutSummary summary = new BuildLayoutSummary();
            foreach (var bundle in layout.Groups)
            {
                {
                    summary.BundleSummary.TotalCompressedSize += bundle.FileSize;
                    summary.BundleSummary.Count++;

                    foreach (var file in bundle.Files)
                    {
                        summary.TotalAssetCount += file.Assets.Count + file.OtherAssets.Count;
                        summary.ExplicitAssetCount += file.Assets.Count;
                        summary.ImplicitAssetCount += file.OtherAssets.Count;
                    }
                }
            }
            return summary;
        }
    }
}
