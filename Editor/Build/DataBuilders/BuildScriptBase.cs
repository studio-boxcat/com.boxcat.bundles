using System;
using System.IO;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Util;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Base class for build script assets
    /// </summary>
    public class BuildScriptBase : ScriptableObject, IDataBuilder
    {
        [NonSerialized]
        internal IBuildLogger m_Log;

        /// <summary>
        /// The descriptive name used in the UI.
        /// </summary>
        public string Name => GetType().Name;

        internal static void WriteBuildLog(BuildLog log, string directory)
        {
            Directory.CreateDirectory(directory);
            var info = PackageManager.PackageInfo.FindForAssembly(typeof(BuildScriptBase).Assembly);
            log.AddMetaData(info.name, info.version);
            File.WriteAllText(Path.Combine(directory, "AddressablesBuildTEP.json"), log.FormatForTraceEventProfiler());
        }

        /// <summary>
        /// Build the specified data with the provided builderInput.  This is the public entry point.
        ///  Child class overrides should use <see cref="BuildDataImplementation{TResult}"/>
        /// </summary>
        /// <typeparam name="TResult">The type of data to build.</typeparam>
        /// <param name="builderInput">The builderInput object used in the build.</param>
        /// <returns>The build data result.</returns>
        public TResult BuildData<TResult>(AddressablesDataBuilderInput builderInput) where TResult : IDataBuilderResult
        {
            if (!CanBuildData<TResult>())
            {
                var message = "Data builder " + Name + " cannot build requested type: " + typeof(TResult);
                Debug.LogError(message);
                return AddressableAssetBuildResult.CreateResult<TResult>(0, message);
            }

            var buildType = AddressableAnalytics.DetermineBuildType();
            m_Log = builderInput.Logger ?? new BuildLog();

            TResult result;
            // Append the file registry to the results
            using (m_Log.ScopedStep(LogLevel.Info, $"Building {Name}"))
            {
                try
                {
                    result = BuildDataImplementation<TResult>(builderInput);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);

                    var errMessage = e.Message == "path"
                        ? "Invalid path detected during build. Check for unmatched brackets in your active profile's variables."
                        : e.Message;

                    return AddressableAssetBuildResult.CreateResult<TResult>(0, errMessage);
                }
            }

            if (builderInput.Logger == null && m_Log != null)
                WriteBuildLog((BuildLog) m_Log, Path.GetDirectoryName(Application.dataPath) + "/" + PathConfig.LibraryPath);

            if (result is AddressableAssetBuildResult buildResult)
            {
                AddressableAnalytics.ReportBuildEvent(builderInput, buildResult, buildType);
            }

            return result;
        }

        /// <summary>
        /// The implementation of <see cref="BuildData{TResult}"/>.  That is the public entry point,
        ///  this is the home for child class overrides.
        /// </summary>
        /// <param name="builderInput">The builderInput object used in the build</param>
        /// <typeparam name="TResult">The type of data to build</typeparam>
        /// <returns>The build data result</returns>
        protected virtual TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput) where TResult : IDataBuilderResult
        {
            return default;
        }

        /// <summary>
        /// Used to determine if this builder is capable of building a specific type of data.
        /// </summary>
        /// <typeparam name="T">The type of data needed to be built.</typeparam>
        /// <returns>True if this builder can build this data.</returns>
        public virtual bool CanBuildData<T>() where T : IDataBuilderResult
        {
            return false;
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
                L.Exception(ex);
                return false;
            }
        }

        /// <summary>
        /// Used to clean up any cached data created by this builder.
        /// </summary>
        public virtual void ClearCachedData()
        {
        }
    }
}