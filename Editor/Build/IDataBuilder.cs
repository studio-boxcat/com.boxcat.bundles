using System;

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