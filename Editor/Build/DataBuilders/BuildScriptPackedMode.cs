using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.BuildReportVisualizer;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Build scripts used for player builds and running with bundles in the editor.
    /// </summary>
    internal class BuildScriptPackedMode : BuildScriptBase
    {
        private const bool _generateBuildReport = false;

        // Tests can set this flag to prevent player script compilation. This is the most expensive part of small builds
        // and isn't needed for most tests.
        private const bool _skinCompilePlayerScripts = false;

        private LinkXmlGenerator m_Linker;

        /// <inheritdoc />
        protected override DataBuildResult BuildDataImplementation(AddressableCatalog catalog, BuildTarget target)
        {
            m_Linker = LinkXmlGenerator.CreateDefault();

            var aaContext = new AddressableAssetsBuildContext(catalog) { buildStartTime = DateTime.Now };
            var result = DoBuild(aaContext, target);

            if (result != null)
            {
                var span = DateTime.Now - aaContext.buildStartTime;
                result.Duration = span.TotalSeconds;
            }

            if (result != null && !Application.isBatchMode && _generateBuildReport)
                BuildReportWindow.ShowWindowAfterBuild();
            return result;
        }

        private static DataBuildResult CreateErrorResult(string errorString, AddressableAssetsBuildContext aaContext)
        {
            BuildLayoutGenerationTask.GenerateErrorReport(errorString, aaContext);
            return new DataBuildResult { Error = errorString };
        }

        private struct SBPSettingsOverwriterScope : IDisposable
        {
            private bool m_PrevSlimResults;

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

            var catalog = aaContext.Catalog;
            var buildParams = BundleBuildParamsFactory.Get(buildTarget);

            L.I("[BuildScriptPackedMode] ContentPipeline.BuildAssetBundles");
            IBundleBuildResults results;
            using (new SBPSettingsOverwriterScope(_generateBuildReport)) // build layout generation requires full SBP write results
            {
                aaContext.bundleToAssetGroup = new Dictionary<BundleKey, AssetGroup>();
                var bundleBuilds = GenerateBundleBuilds(catalog.Groups, aaContext.bundleToAssetGroup);
                var buildContent = new BundleBuildContent(bundleBuilds);
                var buildTasks = RuntimeDataBuildTasks();
                buildTasks.Add(extractData);

                var exitCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out results, buildTasks, aaContext);
                if (exitCode < ReturnCode.Success)
                    return CreateErrorResult("SBP Error" + exitCode, aaContext);
            }

            var bundleIds = ResourceCatalogBuilder.AssignBundleId(aaContext.entries.Values);

            L.I("[BuildScriptPackedMode] PostProcessBundles");
            using (var progressTracker = new ProgressTracker())
            {
                progressTracker.UpdateTask("Post Processing AssetBundles");

                foreach (var bundleKey in bundleIds.Keys)
                {
                    // L.I(bundleKey.ToString());
                    CopyBundleToOutputPath(bundleKey.GetBuildName(), bundleIds[bundleKey].Name());
                }
            }

            {
                L.I("[BuildScriptPackedMode] Process Catalog Entries");
                foreach (var resultValue in results.WriteResults.Values)
                {
                    m_Linker.AddTypes(resultValue.includedTypes);
                    m_Linker.AddSerializedClass(resultValue.includedSerializeReferenceFQN);
                }
            }

            {
                L.I("[BuildScriptPackedMode] Generate Binary Catalog");
                var bytes = ResourceCatalogBuilder.Build(aaContext.entries.Values, bundleIds);
                WriteFile(PathConfig.BuildPath_CatalogBin, bytes);
            }

            {
                L.I("[BuildScriptPackedMode] Generate link");
                var dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(catalog))!;
                m_Linker.Save(Path.Combine(dir, "link.xml"));
            }

            if (_generateBuildReport && extractData.BuildContext != null)
            {
                L.I("[BuildScriptPackedMode] Generate Build Layout");
                using var progressTracker = new ProgressTracker();
                progressTracker.UpdateTask("Generating Build Layout");
                var tasks = new List<IBuildTask> { new BuildLayoutGenerationTask() };
                BuildTasksRunner.Run(tasks, extractData.m_BuildContext);
            }
            else
            {
                L.I("[BuildScriptPackedMode] Skipping Build Layout generation");
            }

            return result;
        }

        /// <summary>
        /// Loops over each group, after doing some data checking.
        /// </summary>
        /// <returns>An error string if there were any problems processing the groups</returns>
        public static List<AssetBundleBuild> GenerateBundleBuilds(
            AssetGroup[] groups,
            Dictionary<BundleKey, AssetGroup> bundleToAssetGroup)
        {
            Assert.IsNotNull(groups, "AddressableAssetGroup list is null");

            var bundleBuilds = new List<AssetBundleBuild>(groups.Length);
            foreach (var group in groups)
            {
                var bundleBuild = group.GenerateAssetBundleBuild();
                bundleBuilds.Add(bundleBuild);
                bundleToAssetGroup.Add(BundleKey.FromBuildName(bundleBuild.assetBundleName), group);
            }
            return bundleBuilds;
        }

        private static IList<IBuildTask> RuntimeDataBuildTasks()
        {
            var buildTasks = new List<IBuildTask>();

            // Setup
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());

            // Player Scripts
            if (!_skinCompilePlayerScripts)
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

        private static string CopyBundleToOutputPath(string srcFileName, string dstFileName)
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