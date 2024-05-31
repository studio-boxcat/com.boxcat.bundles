using System;
using System.IO;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets.Util;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Base class for build script assets
    /// </summary>
    public abstract class BuildScriptBase : IDataBuilder
    {
        /// <summary>
        /// The descriptive name used in the UI.
        /// </summary>
        public string Name => GetType().Name;

        /// <summary>
        /// Build the specified data with the provided builderInput.  This is the public entry point.
        ///  Child class overrides should use <see cref="BuildDataImplementation"/>
        /// </summary>
        /// <returns>The build data result.</returns>
        public DataBuildResult BuildData(AddressableAssetSettings settings, BuildTarget target)
        {
            L.I($"[Addressables] Building {Name}");

            // Append the file registry to the results
            try
            {
                return BuildDataImplementation(settings, target);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new DataBuildResult
                {
                    Error = e.Message == "path"
                        ? "Invalid path detected during build. Check for unmatched brackets in your active profile's variables."
                        : e.Message
                };
            }
        }

        /// <summary>
        /// The implementation of <see cref="BuildData"/>.  That is the public entry point,
        ///  this is the home for child class overrides.
        /// </summary>
        /// <returns>The build data result</returns>
        protected abstract DataBuildResult BuildDataImplementation(AddressableAssetSettings settings, BuildTarget target);

        /// <summary>
        /// Used to clean up any cached data created by this builder.
        /// </summary>
        public virtual void ClearCachedData()
        {
        }

        /// <summary>
        /// Utility method for deleting files.
        /// </summary>
        /// <param name="path">The file path to delete.</param>
        protected static void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Utility method to write a file.  The directory will be created if it does not exist.
        /// </summary>
        /// <param name="path">The path of the file to write.</param>
        /// <param name="content">The content of the file.</param>
        /// <returns>True if the file was written.</returns>
        protected static bool WriteFile(string path, byte[] content)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, content);
                return true;
            }
            catch (Exception ex)
            {
                L.E(ex);
                return false;
            }
        }
    }
}