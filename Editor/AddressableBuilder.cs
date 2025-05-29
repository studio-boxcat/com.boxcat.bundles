using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Sirenix.Utilities;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.Assertions;
using BuildCompression = UnityEngine.BuildCompression;

namespace Bundles.Editor
{
    [AttributeUsage(AttributeTargets.Method), MeansImplicitUse]
    public class AddressablePreBuildCallbackAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method), MeansImplicitUse]
    public class AddressablePostBuildCallbackAttribute : Attribute
    {
        [RequiredSignature, UsedImplicitly]
        private static void Signature(bool result, BuildTarget buildTarget, string buildPath) { }
    }

    /// <summary>
    /// Build scripts used for player builds and running with bundles in the editor.
    /// </summary>
    public static class AddressableBuilder
    {
        /// <summary>
        /// Runs the active player data build script to create runtime data.
        /// See the [BuildPlayerContent](xref:addressables-api-build-player-content) documentation for more details.
        /// </summary>
        public static bool Build(
            AddressableCatalog catalog, BuildTarget target,
            bool generateReport = false)
        {
            L.I("[AddressableBuilder] Build start");

            Assert.IsNotNull(catalog, "AddressableAssetSettings must not be null");

            AddressablesUtils.InvokeAllMethodsWithAttribute<AddressablePreBuildCallbackAttribute>();

            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            var buildPath = PathConfig.GetBuildPath(target);
            var error = DoBuild(catalog, target, buildPath,
                generateReport: generateReport, skipCompilePlayerScripts: false);
            var result = error is null;

            var resultCode = result ? "success" : "error";
            L.I($"[AddressableBuilder] Build {resultCode} (duration : {sw.Elapsed:g})");
            if (!string.IsNullOrEmpty(error)) L.E(error);

            AddressablesUtils.InvokeAllMethodsWithAttribute<AddressablePostBuildCallbackAttribute>(
                result, target, buildPath);

            if (!Application.isBatchMode && generateReport && result)
                BuildReportWindow.ShowWindow();
            return result;
        }

        [Shortcut("Addressables/Build (No Report)")]
        private static void Build()
        {
            Build(
                AddressableCatalog.Default,
                EditorUserBuildSettings.activeBuildTarget,
                generateReport: false);
        }

        [Shortcut("Addressables/Build (With Report)")]
        private static void BuildWithReport()
        {
            Build(
                AddressableCatalog.Default,
                EditorUserBuildSettings.activeBuildTarget,
                generateReport: true);
        }

        private static string DoBuild(
            AddressableCatalog catalog, BuildTarget target, string buildPath,
            bool generateReport, bool skipCompilePlayerScripts)
        {
            var ctx = new AddressableAssetsBuildContext(catalog);
            if (!AddressablesUtils.CheckModifiedScenesAndAskToSave())
                return "Unsaved scenes";

            // cleanup old build data
            AddressablesUtils.DeleteDirectory(buildPath);
            var buildParams = GetBuildParameter(target, buildPath);
            buildParams.WriteLinkXML = true; // See GenerateLinkXml.cs

            using (new SBPSettingsOverwriterScope(generateReport)) // build layout generation requires full SBP write results
            {
                L.I("[AddressableBuilder] ContentPipeline.BuildAssetBundles");
                var buildContent = new BundleBuildContent(catalog.GenerateBundleBuilds());
                var buildTasks = PopulateBuildTasks(
                    generateBuildLayout: generateReport,
                    skipCompilePlayerScripts: skipCompilePlayerScripts);
                var exitCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out _, buildTasks, ctx);
                if (exitCode < ReturnCode.Success) // Success Codes are Positive!
                    return "SBP Error: " + exitCode;
            }

            {
                L.I("[AddressableBuilder] Copy link.xml");
                var srcPath = buildPath + "/link.xml";
                var dstPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(catalog))! + "/link.xml";
                AddressablesUtils.ReplaceFile(srcPath, dstPath);
            }

            {
                L.I("[AddressableBuilder] Generate Binary Catalog");
                ResourceCatalogBuilder.Build(ctx, buildPath + "/catalog.bin");
            }

            return null;
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

        private static IList<IBuildTask> PopulateBuildTasks(
            bool generateBuildLayout, bool skipCompilePlayerScripts)
        {
            var buildTasks = (List<IBuildTask>) DefaultBuildTasks.Create(DefaultBuildTasks.Preset.AssetBundleShaderAndScriptExtraction);

            // remove BuildPlayerScripts when skipCompilePlayerScripts
            if (skipCompilePlayerScripts)
                buildTasks.RemoveAll(x => x is BuildPlayerScripts);

            // do not append hash to bundle names
            buildTasks.RemoveAll(x => x is AppendBundleHash);

            // add GenerateLocationListsTask to the end of the list
            buildTasks.Add(new GenerateLocationListsTask());

            // add BuildLayoutGenerationTask if generateBuildLayout
            if (generateBuildLayout)
                buildTasks.Add(new BuildLayoutGenerationTask());

            // modify tasks
            buildTasks.FilterCast<CreateMonoScriptBundle>().First().MonoScriptBundleName = AssetBundleId.MonoScript.Name();

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
                BundleCompression = BuildCompression.LZ4,
            };

            result.ContentBuildFlags |= ContentBuildFlags.StripUnityVersion;
            return result;
        }
    }
}