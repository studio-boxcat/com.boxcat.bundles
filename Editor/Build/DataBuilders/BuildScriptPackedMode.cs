using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.BuildReportVisualizer;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Build scripts used for player builds and running with bundles in the editor.
    /// </summary>
    public class BuildScriptPackedMode : BuildScriptBase
    {
        LinkXmlGenerator m_Linker;

        /// <inheritdoc />
        protected override DataBuildResult BuildDataImplementation(AddressableAssetSettings settings, BuildTarget target)
        {
            m_Linker = LinkXmlGenerator.CreateDefault();

            var aaContext = new AddressableAssetsBuildContext(settings) { buildStartTime = DateTime.Now };
            var result = DoBuild(aaContext, target);

            if (result != null)
            {
                var span = DateTime.Now - aaContext.buildStartTime;
                result.Duration = span.TotalSeconds;
            }

            if (result != null && !Application.isBatchMode && ProjectConfigData.AutoOpenAddressablesReport && ProjectConfigData.GenerateBuildLayout)
                BuildReportWindow.ShowWindowAfterBuild();
            return result;
        }

        private static DataBuildResult CreateErrorResult(string errorString, AddressableAssetsBuildContext aaContext)
        {
            BuildLayoutGenerationTask.GenerateErrorReport(errorString, aaContext);
            return new DataBuildResult { Error = errorString };
        }

        struct SBPSettingsOverwriterScope : IDisposable
        {
            bool m_PrevSlimResults;

            public SBPSettingsOverwriterScope(bool forceFullWriteResults)
            {
                m_PrevSlimResults = ScriptableBuildPipeline.slimWriteResults;
                if (forceFullWriteResults)
                    ScriptableBuildPipeline.slimWriteResults = false;
            }

            public void Dispose()
            {
                ScriptableBuildPipeline.slimWriteResults = m_PrevSlimResults;
            }
        }

        /// <summary>
        /// The method that does the actual building after all the groups have been processed.
        /// </summary>
        protected DataBuildResult DoBuild(AddressableAssetsBuildContext aaContext, BuildTarget buildTarget)
        {
            var result = new DataBuildResult();
            var extractData = new ExtractDataTask();

            if (!BuildUtility.CheckModifiedScenesAndAskToSave())
                return CreateErrorResult("Unsaved scenes", aaContext);

            var settings = aaContext.Settings;
            var buildParams = settings.GetBuildParams(buildTarget);

            L.I("ContentPipeline.BuildAssetBundles");
            IBundleBuildResults results;
            using (new SBPSettingsOverwriterScope(ProjectConfigData.GenerateBuildLayout)) // build layout generation requires full SBP write results
            {
                aaContext.bundleToAssetGroup = new Dictionary<BundleKey, AddressableAssetGroup>();
                var bundleBuilds = GenerateBundleBuilds(settings.groups, aaContext.bundleToAssetGroup);
                var buildContent = new BundleBuildContent(bundleBuilds);
                var buildTasks = RuntimeDataBuildTasks();
                buildTasks.Add(extractData);

                var exitCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out results, buildTasks, aaContext);
                if (exitCode < ReturnCode.Success)
                    return CreateErrorResult("SBP Error" + exitCode, aaContext);
            }

            var bundleIds = ResourceCatalogBuilder.AssignBundleId(aaContext.entries.Values);

            L.I("PostProcessBundles");
            using (var progressTracker = new ProgressTracker())
            {
                progressTracker.UpdateTask("Post Processing AssetBundles");

                foreach (var bundleKey in bundleIds.Keys)
                {
                    // L.I(bundleKey.ToString());
                    var targetPath = CopyBundleToOutputPath(bundleKey.GetBuildName(), bundleIds[bundleKey].Name());
                    result.AssetBundleBuildResults.Add(
                        new DataBuildResult.BundleBuildResult(bundleKey, targetPath));
                }
            }

            {
                L.I("Process Catalog Entries");
                foreach (var r in results.WriteResults)
                {
                    var resultValue = r.Value;
                    m_Linker.AddTypes(resultValue.includedTypes);
                    m_Linker.AddSerializedClass(resultValue.includedSerializeReferenceFQN);
                }
            }

            {
                L.I("Generate Binary Catalog");
                var bytes = ResourceCatalogBuilder.Build(aaContext.entries.Values, bundleIds);
                WriteFile(PathConfig.BuildPath_CatalogBin, bytes);
            }

            {
                L.I("Generate link");
                m_Linker.Save(PathConfig.BuildPath_LinkXML);
            }

            if (ProjectConfigData.GenerateBuildLayout && extractData.BuildContext != null)
            {
                L.I("Generate Build Layout");
                using var progressTracker = new ProgressTracker();
                progressTracker.UpdateTask("Generating Build Layout");
                var tasks = new List<IBuildTask> { new BuildLayoutGenerationTask() };
                BuildTasksRunner.Run(tasks, extractData.m_BuildContext);
            }

            return result;
        }

        /// <summary>
        /// Loops over each group, after doing some data checking.
        /// </summary>
        /// <returns>An error string if there were any problems processing the groups</returns>
        public static List<AssetBundleBuild> GenerateBundleBuilds(
            List<AddressableAssetGroup> groups,
            Dictionary<BundleKey, AddressableAssetGroup> bundleToAssetGroup)
        {
            Assert.IsNotNull(groups, "AddressableAssetGroup list is null");

            var bundleBuilds = new List<AssetBundleBuild>();
            foreach (var group in groups)
            foreach (var bundleBuild in GenerateBundleBuilds(group))
            {
                bundleBuilds.Add(bundleBuild);
                bundleToAssetGroup.Add(BundleKey.FromBuildName(bundleBuild.assetBundleName), group);
            }
            return bundleBuilds;
        }

        /// <summary>
        /// Processes an AddressableAssetGroup and generates AssetBundle input definitions based on the BundlePackingMode.
        /// </summary>
        /// <param name="assetGroup">The AddressableAssetGroup to be processed.</param>
        /// <returns>The total list of AddressableAssetEntries that were processed.</returns>
        static IEnumerable<AssetBundleBuild> GenerateBundleBuilds(AddressableAssetGroup assetGroup)
        {
            return assetGroup.BundleMode switch
            {
                BundlePackingMode.PackTogether => GenerateBuildInputDefinitions(assetGroup, assetGroup.entries, "all"),
                BundlePackingMode.PackSeparately => assetGroup.entries.SelectMany(e =>
                {
                    var suffix = string.IsNullOrEmpty(e.address) ? e.guid.Value : e.address;
                    return GenerateBuildInputDefinitions(assetGroup, new List<AddressableAssetEntry> { e }, suffix);
                }),
                _ => throw new Exception("Unknown Packing Mode")
            };

            static IEnumerable<AssetBundleBuild> GenerateBuildInputDefinitions(
                AddressableAssetGroup group, ICollection<AddressableAssetEntry> entries, string suffix)
            {
                var scenes = new List<AddressableAssetEntry>();
                var assets = new List<AddressableAssetEntry>();
                foreach (var e in entries)
                    (e.IsScene ? scenes : assets).Add(e);

                if (assets.Count > 0)
                    yield return GenerateBuildInputDefinition(group, assets, "Asset_" + suffix);
                if (scenes.Count > 0)
                    yield return GenerateBuildInputDefinition(group, scenes, "Scene_" + suffix);
            }

            static AssetBundleBuild GenerateBuildInputDefinition(AddressableAssetGroup group, List<AddressableAssetEntry> assets, string suffix)
            {
                return new AssetBundleBuild
                {
                    assetBundleName = BundleKey.Create(group.Name, suffix).GetBuildName(),
                    assetNames = assets.Select(e => e.AssetPath).ToArray(),
                    addressableNames = assets.Select(e => AddressUtils.Hash(e.address).Name()).ToArray()
                };
            }
        }

        // Tests can set this flag to prevent player script compilation. This is the most expensive part of small builds
        // and isn't needed for most tests.
        internal static bool s_SkipCompilePlayerScripts = false;

        static IList<IBuildTask> RuntimeDataBuildTasks()
        {
            var buildTasks = new List<IBuildTask>();

            // Setup
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());

            // Player Scripts
            if (!s_SkipCompilePlayerScripts)
                buildTasks.Add(new BuildPlayerScripts());
            buildTasks.Add(new PostScriptsCallback());

            // Dependency
            buildTasks.Add(new CalculateSceneDependencyData());
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new StripUnusedSpriteSources());
            buildTasks.Add(new CreateBuiltInShadersBundle(BundleNames.BuiltInShaders));
            buildTasks.Add(new CreateMonoScriptBundle(BundleNames.MonoScript));
            buildTasks.Add(new PostDependencyCallback());

            // Packing
            buildTasks.Add(new GenerateBundlePacking());
            buildTasks.Add(new UpdateBundleObjectLayout());
            buildTasks.Add(new GenerateBundleCommands());
            buildTasks.Add(new GenerateSubAssetPathMaps());
            buildTasks.Add(new GenerateBundleMaps());
            buildTasks.Add(new PostPackingCallback());

            // Writing
            buildTasks.Add(new WriteSerializedFiles());
            buildTasks.Add(new ArchiveAndCompressBundles());
            buildTasks.Add(new GenerateLocationListsTask());
            buildTasks.Add(new PostWritingCallback());

            return buildTasks;
        }

        static string CopyBundleToOutputPath(string srcFileName, string dstFileName)
        {
            var srcPath = Path.Combine(PathConfig.TempPath_BundleRoot, srcFileName);
            var dstPath = Path.Combine(PathConfig.BuildPath_BundleRoot, dstFileName);

            // L.I($"Move File: {srcPath} -> {dstPath}");

            var directory = Path.GetDirectoryName(dstPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            else if (File.Exists(dstPath))
                File.Delete(dstPath);
            File.Copy(srcPath, dstPath);

            return dstPath;
        }

        /// <inheritdoc />
        public override void ClearCachedData()
        {
            if (Directory.Exists(PathConfig.BuildPath) is false)
                return;

            try
            {
                DeleteFile(PathConfig.BuildPath_CatalogBin);
                Directory.Delete(PathConfig.BuildPath, true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}