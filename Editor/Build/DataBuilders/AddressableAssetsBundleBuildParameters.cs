using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Content;
using UnityEngine.AddressableAssets;
using BuildCompression = UnityEngine.BuildCompression;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Custom bundle parameter container that provides custom compression settings per bundle.
    /// </summary>
    public class AddressableAssetsBundleBuildParameters : BundleBuildParameters
    {
        /// <summary>
        /// Create a AddressableAssetsBundleBuildParameters with data needed to determine the correct compression per bundle.
        /// </summary>
        /// <param name="aaSettings">The AddressableAssetSettings object to use for retrieving groups.</param>
        /// <param name="target">The build target.  This is used by the BundleBuildParameters base class.</param>
        /// <param name="group">The build target group. This is used by the BundleBuildParameters base class.</param>
        public AddressableAssetsBundleBuildParameters(
            AddressableAssetSettings aaSettings, BuildTarget target, BuildTargetGroup group)
            : base(target, group, PathConfig.TempPath_BundleRoot)
        {
            UseCache = true;
            ContiguousBundles = aaSettings.ContiguousBundles;
#if NONRECURSIVE_DEPENDENCY_DATA
            NonRecursiveDependencies = aaSettings.NonRecursiveBuilding;
#endif
            DisableVisibleSubAssetRepresentations = true;

            BundleCompression = target == BuildTarget.WebGL ? BuildCompression.LZ4Runtime : BuildCompression.LZMA;

            ContentBuildFlags |= ContentBuildFlags.StripUnityVersion;
        }
    }
}