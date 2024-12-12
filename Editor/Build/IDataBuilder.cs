using System;
using System.Collections.Generic;

namespace UnityEditor.AddressableAssets.Build
{
    /// <summary>
    /// Contains information about the status of the build.
    /// </summary>
    [Serializable]
    public class DataBuildResult
    {
        /// <summary>
        /// Duration of build, in seconds.
        /// </summary>
        public double Duration;

        /// <summary>
        /// Error that caused the build to fail.
        /// </summary>
        public string Error;

        /// <summary>
        /// Build results for AssetBundles created during the build.
        /// </summary>
        public List<BundleBuildResult> AssetBundleBuildResults = new();

        /// <summary>
        /// Information about a bundle build results
        /// </summary>
        [Serializable]
        public class BundleBuildResult
        {
            /// <summary>
            /// The Addressable Group that was responsible for generating a given AssetBundle
            /// </summary>
            public BundleKey BundleKey;
            /// <summary>
            /// The file path of the bundle.
            /// </summary>
            public string FilePath;

            public BundleBuildResult(BundleKey bundleKey, string filePath)
            {
                BundleKey = bundleKey;
                FilePath = filePath;
            }
        }
    }

    /// <summary>
    /// Builds objects of type DataBuildResult.
    /// </summary>
    public interface IDataBuilder
    {
        /// <summary>
        /// The name of the builder, used for GUI.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Build the data of a specific type.
        /// </summary>
        /// <returns>The built data.</returns>
        DataBuildResult BuildData(AddressableCatalog catalog, BuildTarget target);

        /// <summary>
        /// Clears all cached data.
        /// </summary>
        void ClearCachedData();
    }
}