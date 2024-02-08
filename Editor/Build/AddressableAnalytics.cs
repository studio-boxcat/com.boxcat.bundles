using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.ResourceManagement.Util;

namespace UnityEditor.AddressableAssets
{
    internal static class AddressableAnalytics
    {
        private const string VendorKey = "unity.addressables";
        private static HashSet<string> _registeredEvents = new HashSet<string>();

        private const string UsageEvent = "addressablesUsageEvent";
        private const string BuildEvent = "addressablesBuildEvent";

        [Serializable]
        internal struct BuildData
        {
            public bool IsContentUpdateBuild;
            public bool DebugBuildLayoutEnabled;
            public bool AutoOpenBuildReportEnabled;
            public bool BuildAndRelease;
            public bool IsPlayModeBuild;
            public int BuildScript;
            public int NumberOfAddressableAssets;
            public int NumberOfAssetBundles;
            public int NumberOfGroups;
            public int NumberOfGroupsUsingLZ4;
            public int NumberOfGroupsUsingLZMA;
            public int NumberOfGroupsUncompressed;
            public int NumberOfGroupsPackedTogether;
            public int NumberOfGroupsPackedSeparately;
            public int MaxNumberOfAddressableAssetsInAGroup;
            public int MinNumberOfAddressableAssetsInAGroup;
            public int BuildTarget;

            public int IsIncrementalBuild;
            public int ErrorCode;
            public double TotalBuildTime;
        }

        private static string GetSessionStateKeyByUsageEventType(UsageEventType uet)
        {
            switch (uet)
            {
                case UsageEventType.OpenAnalyzeWindow:
                    return "Addressables/Analyze";
                case UsageEventType.OpenGroupsWindow:
                    return "Addressables/Groups";
                case UsageEventType.OpenHostingWindow:
                    return "Addressables/Hosting";
                case UsageEventType.OpenProfilesWindow:
                    return "Addressables/Profiles";
                case UsageEventType.OpenEventViewerWindow:
                    return "Addressables/EventViewer";
                default:
                    return null;
            }
        }

        internal struct UsageData
        {
            public int UsageEventType;
        }

        internal enum UsageEventType
        {
            OpenGroupsWindow = 0,
            OpenProfilesWindow = 1,
            OpenEventViewerWindow = 2,
            OpenAnalyzeWindow = 3,
            OpenHostingWindow = 4,
            RunBundleLayoutPreviewRule = 5,
            RunCheckBundleDupeDependenciesRule = 6,
            RunCheckResourcesDupeDependenciesRule = 7,
            RunCheckSceneDupeDependenciesRule = 8,
            ContentUpdateCancelled = 10,
            ContentUpdateHasChangesInUpdateRestrictionWindow = 11,
            ContentUpdateContinuesWithoutChanges = 12,
            RunContentUpdateBuild = 13,
            CannotLocateBinFile = 14,
            BuildFailedDueToModifiedStaticEntries = 15,
            BuildInterruptedDueToStaticModifiedEntriesInUpdate = 16,
            OpenBuildReportManually = 17,
            BuildReportSelectedExploreTab = 18,
            BuildReportSelectedPotentialIssuesTab = 19,
            BuildReportSelectedSummaryTab = 20,
            BuildReportViewByAssetBundle = 21,
            BuildReportViewByAssets = 22,
            BuildReportViewByGroup = 24,
            BuildReportViewByDuplicatedAssets = 25,
            BuildReportImportedManually = 26
        }

        internal enum BuildScriptType
        {
            PackedMode = 0,
            PackedPlayMode = 1,
            FastMode = 2,
            CustomBuildScript = 4
        }

        internal enum BuildType
        {
            Inconclusive = -1,
            CleanBuild = 0,
            IncrementalBuild = 1
        }

        internal enum ErrorType
        {
            NoError = 0,
            GenericError = 1
        }

        private static bool EventIsRegistered(string eventName)
        {
            return _registeredEvents.Contains(eventName);
        }

        //Check if the build cache exists so we know if a build is incremental or clean. May return inconclusive if reflection fails
        internal static BuildType DetermineBuildType()
        {
            try
            {
                FieldInfo cachePathField = typeof(BuildCache).GetField("k_CachePath", BindingFlags.Static | BindingFlags.NonPublic);
                if (cachePathField == null)
                    return BuildType.Inconclusive;
                string cachePath = (string)cachePathField.GetValue(null);
                if (cachePath != null && Directory.Exists(cachePath))
                    return BuildType.IncrementalBuild;
                if (cachePath != null)
                    return BuildType.CleanBuild;
                return BuildType.Inconclusive;
            }
            catch
            {
                return BuildType.Inconclusive;
            }
        }

        internal static BuildScriptType DetermineBuildScriptType(IDataBuilder buildScript)
        {
            if (buildScript == null)
                return BuildScriptType.CustomBuildScript;

            var type = buildScript.GetType();
            if (type == typeof(BuildScriptPackedMode))
                return BuildScriptType.PackedMode;
            if (type == typeof(BuildScriptFastMode))
                return BuildScriptType.FastMode;
            if (type == typeof(BuildScriptPackedPlayMode))
                return BuildScriptType.PackedPlayMode;
            return BuildScriptType.CustomBuildScript;
        }

        internal static ErrorType ParseError(string error)
        {
            if (String.IsNullOrEmpty(error))
                return ErrorType.NoError;
            return ErrorType.GenericError;
        }

        internal static BuildData GenerateBuildData(AddressablesDataBuilderInput builderInput, AddressableAssetBuildResult result, BuildType buildType)
        {
            AddressableAssetSettings currentSettings = builderInput.AddressableSettings;
            bool isContentUpdateBuild = builderInput.IsContentUpdateBuild;
            bool isBuildAndRelease = builderInput.IsBuildAndRelease;

            string error = result.Error;
            bool isPlayModeBuild = result is AddressablesPlayModeBuildResult;
            double totalBuildDurationSeconds = result.Duration;
            int numberOfAssetBundles = -1;

            if (result is AddressablesPlayerBuildResult buildRes)
            {
                numberOfAssetBundles = buildRes.AssetBundleBuildResults.Count;
            }

            ErrorType errorCode = ParseError(error);

            if (isPlayModeBuild)
            {
                BuildScriptType playModeBuildScriptType = DetermineBuildScriptType(currentSettings.ActivePlayModeDataBuilder);
                BuildData playModeBuildData = new BuildData()
                {
                    IsPlayModeBuild = true,
                    BuildScript = (int)playModeBuildScriptType
                };

                return playModeBuildData;
            }

            BuildScriptType buildScriptType = DetermineBuildScriptType(currentSettings.ActivePlayerDataBuilder);
            int numberOfAddressableAssets = 0;

            int numberOfGroupsUncompressed = 0;
            int numberOfGroupsUsingLZMA = 0;
            int numberOfGroupsUsingLZ4 = 0;

            int numberOfGroupsPackedSeparately = 0;
            int numberOfGroupsPackedTogether = 0;

            int minNumberOfAssetsInAGroup = -1;
            int maxNumberOfAssetsInAGroup = -1;


            foreach (var group in currentSettings.groups)
            {
                if (group == null)
                    continue;

                numberOfAddressableAssets += group.entries.Count;

                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null)
                    continue;

                var bundleMode = schema.BundleMode;
                var compressionType = schema.Compression;

                switch (compressionType)
                {
                    case BundledAssetGroupSchema.BundleCompressionMode.Uncompressed:
                        numberOfGroupsUncompressed += 1;
                        break;
                    case BundledAssetGroupSchema.BundleCompressionMode.LZ4:
                        numberOfGroupsUsingLZ4 += 1;
                        break;
                    case BundledAssetGroupSchema.BundleCompressionMode.LZMA:
                        numberOfGroupsUsingLZMA += 1;
                        break;
                }

                switch (bundleMode)
                {
                    case BundledAssetGroupSchema.BundlePackingMode.PackSeparately:
                        numberOfGroupsPackedSeparately += 1;
                        break;
                    case BundledAssetGroupSchema.BundlePackingMode.PackTogether:
                        numberOfGroupsPackedTogether += 1;
                        break;
                }

                if (group.entries.Count > maxNumberOfAssetsInAGroup)
                    maxNumberOfAssetsInAGroup = group.entries.Count;
                if (minNumberOfAssetsInAGroup == -1 || group.entries.Count < minNumberOfAssetsInAGroup)
                    minNumberOfAssetsInAGroup = group.entries.Count;
            }

            BuildData data = new BuildData()
            {
                IsContentUpdateBuild = isContentUpdateBuild,
                IsPlayModeBuild = false,
                BuildScript = (int)buildScriptType,
                BuildAndRelease = isBuildAndRelease,
#if UNITY_2022_2_OR_NEWER
                DebugBuildLayoutEnabled = ProjectConfigData.GenerateBuildLayout,
                AutoOpenBuildReportEnabled = ProjectConfigData.AutoOpenAddressablesReport && ProjectConfigData.GenerateBuildLayout,
#endif
                IsIncrementalBuild = (int)buildType,
                NumberOfAssetBundles = numberOfAssetBundles,
                NumberOfAddressableAssets = numberOfAddressableAssets,
                MinNumberOfAddressableAssetsInAGroup = minNumberOfAssetsInAGroup,
                MaxNumberOfAddressableAssetsInAGroup = maxNumberOfAssetsInAGroup,
                NumberOfGroups = currentSettings.groups.Count,
                TotalBuildTime = totalBuildDurationSeconds,
                NumberOfGroupsUsingLZ4 = numberOfGroupsUsingLZ4,
                NumberOfGroupsUsingLZMA = numberOfGroupsUsingLZMA,
                NumberOfGroupsUncompressed = numberOfGroupsUncompressed,
                NumberOfGroupsPackedTogether = numberOfGroupsPackedTogether,
                NumberOfGroupsPackedSeparately = numberOfGroupsPackedSeparately,
                BuildTarget = (int)EditorUserBuildSettings.activeBuildTarget,
                ErrorCode = (int)errorCode
            };

            return data;
        }

        internal static void ReportBuildEvent(AddressablesDataBuilderInput builderInput, AddressableAssetBuildResult result, BuildType buildType)
        {
            if (!EditorAnalytics.enabled)
                return;

            if (!EventIsRegistered(BuildEvent))
                if (!RegisterEvent(BuildEvent))
                    return;

            BuildData data = GenerateBuildData(builderInput, result, buildType);
            EditorAnalytics.SendEventWithLimit(BuildEvent, data);
        }

        internal static UsageData GenerateUsageData(UsageEventType eventType)
        {
            var data = new UsageData()
            {
                UsageEventType = (int)eventType,
            };

            return data;
        }

        internal static void ReportUsageEvent(UsageEventType eventType, bool limitEventOncePerSession = false)
        {
            if (!EditorAnalytics.enabled)
                return;

            var sessionStateKey = GetSessionStateKeyByUsageEventType(eventType);

            if (limitEventOncePerSession && sessionStateKey != null && SessionState.GetBool(sessionStateKey, false))
                return;

            if (!SessionState.GetBool(sessionStateKey, false))
                SessionState.SetBool(sessionStateKey, true);

            if (!EventIsRegistered(UsageEvent))
                if (!RegisterEvent(UsageEvent))
                    return;

            UsageData data = GenerateUsageData(eventType);
            EditorAnalytics.SendEventWithLimit(UsageEvent, data);
        }

        private static bool RegisterEvent(string eventName)
        {
            bool eventSuccessfullyRegistered = false;
            AnalyticsResult registerEvent = EditorAnalytics.RegisterEventWithLimit(eventName, 100, 100, VendorKey);
            if (registerEvent == AnalyticsResult.Ok)
            {
                _registeredEvents.Add(eventName);
                eventSuccessfullyRegistered = true;
            }

            return eventSuccessfullyRegistered;
        }
    }
}
