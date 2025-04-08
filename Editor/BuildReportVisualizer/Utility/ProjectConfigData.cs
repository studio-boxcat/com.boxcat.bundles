using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// The project configuration settings for addressables.
    /// </summary>
    internal static class ProjectConfigData
    {
        private static List<string> s_BuildReportFilePaths;

        /// <summary>
        /// Returns the file paths of build reports used by the Build Reports window.
        /// </summary>
        public static List<string> BuildReportFilePaths
        {
            get
            {
                LoadDataIfNecessary();
                return s_BuildReportFilePaths;
            }
        }

        /// <summary>
        /// Adds the filepath of a build report to be used by the Build Reports window
        /// </summary>
        /// <param name="reportFilePath">The file path to add</param>
        public static void AddBuildReportFilePath(string reportFilePath)
        {
            LoadDataIfNecessary();
            s_BuildReportFilePaths.Add(reportFilePath);
            SaveData();
        }

        /// <summary>
        /// Removes the build report at index from the list of build reports shown in the Build Reports window
        /// </summary>
        /// <param name="index">The index of the build report to be removed</param>
        public static void RemoveBuildReportFilePathAtIndex(int index)
        {
            LoadDataIfNecessary();
            s_BuildReportFilePaths.RemoveAt(index);
            SaveData();
        }

        /// <summary>
        /// Removes all build reports from the Build Reports window
        /// </summary>
        public static void ClearBuildReportFilePaths()
        {
            LoadDataIfNecessary();
            s_BuildReportFilePaths.Clear();
            SaveData();
        }


        private static void LoadDataIfNecessary()
        {
            s_BuildReportFilePaths ??= PlayerPrefs.GetString("5hhd6lgT", "")
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        private static void SaveData()
        {
            LoadDataIfNecessary();
            PlayerPrefs.SetString("5hhd6lgT", string.Join(";", s_BuildReportFilePaths));
        }
    }
}