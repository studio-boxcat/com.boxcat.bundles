using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine.AddressableAssets;
using BuildCompression = UnityEngine.BuildCompression;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Custom bundle parameter container that provides custom compression settings per bundle.
    /// </summary>
    public static class AddressableAssetsBundleBuildParameters
    {
        public static IBundleBuildParameters GetBuildParams(this AddressableAssetSettings settings, BuildTarget target)
        {
            var result = new BundleBuildParameters(
                target, BuildPipeline.GetBuildTargetGroup(target), PathConfig.TempPath_BundleRoot)
            {
                UseCache = true,
                ContiguousBundles = settings.ContiguousBundles,
#if NONRECURSIVE_DEPENDENCY_DATA
                NonRecursiveDependencies = settings.NonRecursiveBuilding,
#endif
                DisableVisibleSubAssetRepresentations = true,
                BundleCompression = target == BuildTarget.WebGL ? BuildCompression.LZ4Runtime : BuildCompression.LZMA,
            };

            result.ContentBuildFlags |= ContentBuildFlags.StripUnityVersion;
            return result;
        }
    }
}