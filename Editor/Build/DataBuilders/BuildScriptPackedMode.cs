using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirenix.Utilities;
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
    /// Build scripts used for player builds and running with bundles in the editor.
    /// </summary>
    internal static class BuildScriptPackedMode
    {
        private const bool _generateBuildReport = false;

        // Tests can set this flag to prevent player script compilation. This is the most expensive part of small builds
        // and isn't needed for most tests.
        private const bool _skipCompilePlayerScripts = false;

        public static DataBuildResult BuildData(AddressableCatalog catalog, BuildTarget target)
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            var result = DoBuild(catalog, target);
            result.Duration = sw.Elapsed.TotalSeconds;
            if (!Application.isBatchMode && _generateBuildReport)
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
        private static DataBuildResult DoBuild(AddressableCatalog catalog, BuildTarget buildTarget)
        {
            var result = new DataBuildResult();
            var extractData = new ExtractDataTask();

            var ctx = new AddressableAssetsBuildContext(catalog);
            if (!BuildUtility.CheckModifiedScenesAndAskToSave())
                return CreateErrorResult("Unsaved scenes", ctx);

            // cleanup old build data
            var outDir = PathConfig.BuildPath;
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            var buildParams = GetBuildParameter(buildTarget, outDir);
            buildParams.WriteLinkXML = true; // See GenerateLinkXml.cs

            L.I("[BuildScriptPackedMode] ContentPipeline.BuildAssetBundles");
            using (new SBPSettingsOverwriterScope(_generateBuildReport)) // build layout generation requires full SBP write results
            {
                var buildContent = new BundleBuildContent(catalog.GenerateBundleBuilds());
                var buildTasks = RuntimeDataBuildTasks();
                buildTasks.Add(extractData);

                var exitCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out _, buildTasks, ctx);
                if (exitCode < ReturnCode.Success)
                    return CreateErrorResult("SBP Error" + exitCode, ctx);
            }

            var bundleIds = ResourceCatalogBuilder.BuildBundleIdMap(catalog);

            L.I("[BuildScriptPackedMode] Copy link.xml");
            {
                var srcPath = outDir + "/link.xml";
                var dstPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(catalog))! + "/link.xml";
                AssetDatabase.DeleteAsset(dstPath);
                File.Copy(srcPath, dstPath);
            }

            L.I("[BuildScriptPackedMode] Generate Binary Catalog");
            {
                var bytes = ResourceCatalogBuilder.Build(ctx.entries.Values, bundleIds);
                File.WriteAllBytes(outDir + "/catalog.bin", bytes); // if this file exists, overwrite it
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

            // modify tasks
            buildTasks.FilterCast<CreateMonoScriptBundle>().First().MonoScriptBundleName = BundleNames.MonoScript;
            buildTasks.FilterCast<CreateBuiltInShadersBundle>().First().ShaderBundleName = BundleNames.BuiltInShaders;
            return buildTasks;
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