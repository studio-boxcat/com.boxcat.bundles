using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine.AddressableAssets;
using BuildCompression = UnityEngine.BuildCompression;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    static class BundleBuildParamsFactory
    {
        public static IBundleBuildParameters Get(BuildTarget target)
        {
            var result = new BundleBuildParameters(
                target, BuildPipeline.GetBuildTargetGroup(target), PathConfig.TempPath_BundleRoot)
            {
                UseCache = true,
                // If set, Calculates and build asset bundles using Non-Recursive Dependency calculation methods.
                // This approach helps reduce asset bundle rebuilds and runtime memory consumption.
                NonRecursiveDependencies = false,
                // If set, packs assets in bundles contiguously based on the ordering of the source asset
                // which results in improved asset loading times. Disable this if you've built bundles with
                // a version of Addressables older than 1.12.1 and you want to minimize bundle changes.
                ContiguousBundles = true,
                DisableVisibleSubAssetRepresentations = false, // To include main sprite in Texture.
                // LZMA: This compression format is a stream of data representing the entire AssetBundle,
                // which means that if you need to read an Asset from these archives, you must decompress the entire stream.
                // LZ4: compression is a chunk-based compression algorithm.
                // If Unity needs to access an Asset from an LZ4 archive,
                // it only needs to decompress and read the chunks that contain bytes of the requested Asset.
                // XXX: BuildCompression.LZMA 를 사용하는 경우, 다수의 애셋을 동시로드하면 안드로이드에서 데드락이 걸림.
                BundleCompression = BuildCompression.LZMA,
            };

            result.ContentBuildFlags |= ContentBuildFlags.StripUnityVersion;
            return result;
        }
    }
}