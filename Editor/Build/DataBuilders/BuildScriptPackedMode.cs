using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.BuildReportVisualizer;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using BuildCompression = UnityEngine.BuildCompression;

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
        private const bool _skipCompilePlayerScripts = false;

        /// <inheritdoc />
        protected override DataBuildResult BuildDataImplementation(AddressableCatalog catalog, BuildTarget target)
        {
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
            var buildParams = GetBuildParameter(buildTarget, PathConfig.TempPath_BundleRoot);
            buildParams.WriteLinkXML = true; // See GenerateLinkXml.cs

            L.I("[BuildScriptPackedMode] ContentPipeline.BuildAssetBundles");
            IBundleBuildResults results;
            using (new SBPSettingsOverwriterScope(_generateBuildReport)) // build layout generation requires full SBP write results
            {
                var buildContent = new BundleBuildContent(catalog.GenerateBundleBuilds());
                var buildTasks = RuntimeDataBuildTasks();
                buildTasks.Add(extractData);

                var exitCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out results, buildTasks, aaContext);
                if (exitCode < ReturnCode.Success)
                    return CreateErrorResult("SBP Error" + exitCode, aaContext);
            }

            var bundleIds = ResourceCatalogBuilder.BuildBundleIdMap(catalog);

            L.I("[BuildScriptPackedMode] Copy to Output Folder");
            using (var progressTracker = new ProgressTracker())
            {
                progressTracker.UpdateTask("Copy to Output Folder");

                // bundles
                foreach (var bundleKey in bundleIds.Keys)
                {
                    // L.I(bundleKey.ToString());
                    Overwrite(
                        Path.Combine(PathConfig.TempPath_BundleRoot, bundleKey.Value),
                        Path.Combine(PathConfig.BuildPath_BundleRoot, bundleIds[bundleKey].Name()));
                }

                // link.xml
                {
                    Overwrite(
                        Path.Combine(PathConfig.TempPath_BundleRoot, "link.xml"),
                        Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(catalog))!, "link.xml"));
                }
            }

            {
                L.I("[BuildScriptPackedMode] Generate Binary Catalog");
                var bytes = ResourceCatalogBuilder.Build(aaContext.entries.Values, bundleIds);
                WriteFile(PathConfig.BuildPath_CatalogBin, bytes);
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

        private static IList<IBuildTask> RuntimeDataBuildTasks()
        {
            var buildTasks = (List<IBuildTask>) DefaultBuildTasks.Create(DefaultBuildTasks.Preset.AssetBundleShaderAndScriptExtraction);

            // remove BuildPlayerScripts when skipCompilePlayerScripts
            if (_skipCompilePlayerScripts)
                buildTasks.RemoveAll(x => x is BuildPlayerScripts);

            // do not append hash to bundle names
            buildTasks.RemoveAll(x => x is AppendBundleHash);

            // add GenerateLocationListsTask to the end of the list
            buildTasks.Add(new GenerateLocationListsTask());

            return buildTasks;
        }

        private static string Overwrite(string srcPath, string dstPath)
        {
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

        public static IBundleBuildParameters GetBuildParameter(BuildTarget target, string outputFolder)
        {
            var result = new BundleBuildParameters(
                target, BuildPipeline.GetBuildTargetGroup(target), outputFolder)
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
                // XXX: BuildCompression.LZ4 를 사용하는 경우, 다수의 애셋을 동시로드하면 안드로이드에서 데드락이 걸림.
                BundleCompression = BuildCompression.LZMA,
            };

            result.ContentBuildFlags |= ContentBuildFlags.StripUnityVersion;
            return result;
        }
    }
}