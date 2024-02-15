using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets.Settings
{
    /// <summary>
    /// The project configuration settings for addressables.
    /// </summary>
    public class ProjectConfigData
    {
        [Serializable]
        class ConfigSaveData
        {
            // XXX: Unused but keeping for ConfigSaveData compatibility.
            [SerializeField]
            internal long localLoadSpeedInternal = 1024 * 1024 * 10;

            // XXX: Unused but keeping for ConfigSaveData compatibility.
            [SerializeField]
            internal bool hierarchicalSearchInternal = false;

            [SerializeField]
            internal int activePlayModeIndex = 0;

            // XXX: Unused but keeping for ConfigSaveData compatibility.
            [SerializeField]
            internal bool showGroupsAsHierarchy = false;

            [SerializeField]
            internal bool generateBuildLayout = false;

            [SerializeField]
            internal ReportFileFormat buildLayoutReportFileFormat = ReportFileFormat.JSON;

            [SerializeField]
            internal List<string> buildReports = new();

#if UNITY_2022_2_OR_NEWER
            [SerializeField]
            internal bool autoOpenAddressablesReport = true;
            [SerializeField]
            internal bool userHasBeenInformedAboutBuildReportSettingPreBuild = false;
#endif
        }

        static ConfigSaveData s_Data;

        /// <summary>
        /// Whether to generate the bundle build layout report.
        /// </summary>
        public static bool GenerateBuildLayout
        {
            get
            {
                ValidateData();
                return s_Data.generateBuildLayout;
            }
            set
            {
                ValidateData();
                if (s_Data.generateBuildLayout != value)
                {
                    s_Data.generateBuildLayout = value;
                    SaveData();
                }
            }
        }

#if UNITY_2022_2_OR_NEWER
        internal static bool AutoOpenAddressablesReport
        {
            get
            {
                ValidateData();
                return s_Data.autoOpenAddressablesReport;
            }
            set
            {
                ValidateData();
                if (s_Data.autoOpenAddressablesReport != value)
                {
                    s_Data.autoOpenAddressablesReport = value;
                    SaveData();
                }
            }
        }

        internal static bool UserHasBeenInformedAboutBuildReportSettingPreBuild
        {
            get
            {
                ValidateData();
                return s_Data.userHasBeenInformedAboutBuildReportSettingPreBuild;
            }
            set
            {
                ValidateData();
                if (s_Data.userHasBeenInformedAboutBuildReportSettingPreBuild != value)
                {
                    s_Data.userHasBeenInformedAboutBuildReportSettingPreBuild = value;
                    SaveData();
                }
            }
        }
#endif

        /// <summary>
        /// File formats supported for the bundle build layout report.
        /// </summary>
        public enum ReportFileFormat
        {
            /// <summary>
            /// When selected, a human readable .txt build layout will be generated alongside the .json file format
            /// </summary>
            TXT,

            /// <summary>
            /// The .json file format.
            /// </summary>
            JSON
        };

        /// <summary>
        /// File format of the bundle build layout report.
        /// </summary>
        public static ReportFileFormat BuildLayoutReportFileFormat
        {
            get
            {
                ValidateData();
                return s_Data.buildLayoutReportFileFormat;
            }
            set
            {
                ValidateData();
                if (s_Data.buildLayoutReportFileFormat != value)
                {
                    s_Data.buildLayoutReportFileFormat = value;
                    SaveData();
                }
            }
        }

        /// <summary>
        /// Returns the file paths of build reports used by the Build Reports window.
        /// </summary>
        public static List<string> BuildReportFilePaths
        {
            get
            {
                ValidateData();
                return s_Data.buildReports;
            }
        }

        /// <summary>
        /// Adds the filepath of a build report to be used by the Build Reports window
        /// </summary>
        /// <param name="reportFilePath">The file path to add</param>
        public static void AddBuildReportFilePath(string reportFilePath)
        {
            ValidateData();
            s_Data.buildReports.Add(reportFilePath);
            SaveData();
        }

        /// <summary>
        /// Removes the build report at index from the list of build reports shown in the Build Reports window
        /// </summary>
        /// <param name="index">The index of the build report to be removed</param>
        public static void RemoveBuildReportFilePathAtIndex(int index)
        {
            ValidateData();
            s_Data.buildReports.RemoveAt(index);
            SaveData();
        }

        /// <summary>
        /// Removes all build reports from the Build Reports window
        /// </summary>
        public static void ClearBuildReportFilePaths()
        {
            ValidateData();
            s_Data.buildReports.Clear();
            SaveData();
        }


        /// <summary>
        /// The active play mode data builder index.
        /// </summary>
        public static byte ActivePlayModeIndex
        {
            get
            {
                ValidateData();
                return (byte) s_Data.activePlayModeIndex;
            }
            set
            {
                ValidateData();
                s_Data.activePlayModeIndex = value;
                SaveData();
            }
        }

        static void ValidateData()
        {
            if (s_Data != null) return;

            var dataPath = Path.GetFullPath(".");
            dataPath = dataPath.Replace("\\", "/");
            dataPath += "/Library/AddressablesConfig.dat";

            if (File.Exists(dataPath))
            {
                var bf = new BinaryFormatter();
                try
                {
                    using var file = new FileStream(dataPath, FileMode.Open, FileAccess.Read);
                    if (bf.Deserialize(file) is ConfigSaveData data)
                        s_Data = data;
                }
                catch
                {
                    //if the current class doesn't match what's in the file, Deserialize will throw. since this data is non-critical, we just wipe it
                    L.W("Error reading Addressable Asset project config (play mode, etc.). Resetting to default.");
                    File.Delete(dataPath);
                }
            }

            //check if some step failed.
            if (s_Data == null)
            {
                s_Data = new ConfigSaveData();
            }

            if (s_Data.buildReports == null)
                s_Data.buildReports = new List<string>();
        }

        static void SaveData()
        {
            if (s_Data == null)
                return;

            var dataPath = Path.GetFullPath(".");
            dataPath = dataPath.Replace("\\", "/");
            dataPath += "/Library/AddressablesConfig.dat";

            var bf = new BinaryFormatter();
            var file = File.Create(dataPath);
            bf.Serialize(file, s_Data);
            file.Close();
        }
    }
}