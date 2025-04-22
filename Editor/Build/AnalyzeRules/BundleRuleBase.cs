using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.Utilities;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    /// <summary>
    /// Base class for handling analyzing bundle rules tasks and checking dependencies
    /// </summary>
    internal class BundleRuleBase : AnalyzeRule
    {
        [NonSerialized]
        internal Dictionary<string, List<GUID>> m_ResourcesToDependencies = new();

        [NonSerialized]
        internal AssetBundleBuild[] m_AllBundleInputDefs = null;

        [NonSerialized]
        internal ExtractDataTask m_ExtractData = null;

        /// <summary>
        /// The BuildTask used to extract write data from the build.
        /// </summary>
        protected ExtractDataTask ExtractData => m_ExtractData;
        /// <summary>
        /// A mapping of resources to a list of guids that correspond to their dependencies
        /// </summary>
        protected Dictionary<string, List<GUID>> ResourcesToDependencies => m_ResourcesToDependencies;
        protected internal AssetBundleBuild[] AllBundleInputDefs => m_AllBundleInputDefs;

        internal static List<IBuildTask> RuntimeDataBuildTasks()
        {
            var buildTasks = (List<IBuildTask>) DefaultBuildTasks.Create(DefaultBuildTasks.Preset.AssetBundleShaderAndScriptExtraction);
            buildTasks.RemoveAll(x => x
                is WriteSerializedFiles
                or ArchiveAndCompressBundles
                or AppendBundleHash
                or GenerateLinkXml
                or PostWritingCallback);
            buildTasks.Add(new GenerateLocationListsTask());
            buildTasks.FilterCast<CreateMonoScriptBundle>().First().MonoScriptBundleName = AssetBundleId.MonoScript.Name();
            return buildTasks;
        }

        /// <summary>
        /// Refresh build to check bundles against current rules
        /// </summary>
        /// <param name="buildContext"> Context information for building</param>
        /// <returns> The return code of whether analyze build was successful, </returns>
        protected internal ReturnCode RefreshBuild(AddressableAssetsBuildContext buildContext)
        {
            var buildParams = AddressableBuilder.GetBuildParameter(
                EditorUserBuildSettings.activeBuildTarget, PathConfig.TempPath);
            var buildTasks = RuntimeDataBuildTasks();
            m_ExtractData = new ExtractDataTask();
            buildTasks.Add(m_ExtractData);

            return ContentPipeline.BuildAssetBundles(buildParams, new BundleBuildContent(m_AllBundleInputDefs),
                out _, buildTasks, buildContext);
        }

        /// <summary>
        /// Get dependencies from bundles
        /// </summary>
        /// <returns> The list of GUIDs of bundle dependencies</returns>
        protected List<GUID> GetAllBundleDependencies()
        {
            if (m_ExtractData == null)
            {
                Debug.LogError("Build not run, RefreshBuild needed before GetAllBundleDependencies");
                return new List<GUID>();
            }

            var explicitGuids = m_ExtractData.WriteData.AssetToFiles.Keys;
            var implicitGuids = GetImplicitGuidToFilesMap().Keys;
            var allBundleGuids = explicitGuids.Union(implicitGuids);

            return allBundleGuids.ToList();
        }

        /// <summary>
        /// Add Resource and Bundle dependencies in common to map of resources to dependencies
        /// </summary>
        /// <param name="bundleDependencyGuids"> GUID list of bundle dependencies</param>
        protected internal void IntersectResourcesDepedenciesWithBundleDependencies(List<GUID> bundleDependencyGuids)
        {
            foreach (var key in m_ResourcesToDependencies.Keys)
            {
                var bundleDependencies = bundleDependencyGuids.Intersect(m_ResourcesToDependencies[key]).ToList();

                m_ResourcesToDependencies[key].Clear();
                m_ResourcesToDependencies[key].AddRange(bundleDependencies);
            }
        }

        /// <summary>
        /// Build map of resources to corresponding dependencies
        /// </summary>
        /// <param name="resourcePaths"> Array of resource paths</param>
        protected internal void BuiltInResourcesToDependenciesMap(string[] resourcePaths)
        {
            for (int sceneIndex = 0; sceneIndex < resourcePaths.Length; ++sceneIndex)
            {
                string path = resourcePaths[sceneIndex];
                if (EditorUtility.DisplayCancelableProgressBar("Generating built-in resource dependency map",
                        "Checking " + path + " for duplicates with Addressables content.",
                        (float) sceneIndex / resourcePaths.Length))
                {
                    m_ResourcesToDependencies.Clear();
                    EditorUtility.ClearProgressBar();
                    return;
                }

                string[] dependencies;
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    using (var w = new BuildInterfacesWrapper())
                    {
                        var usageTags = new BuildUsageTagSet();
                        BuildSettings settings = new BuildSettings
                        {
                            group = EditorUserBuildSettings.selectedBuildTargetGroup,
                            target = EditorUserBuildSettings.activeBuildTarget,
                            typeDB = null,
                            buildFlags = ContentBuildFlags.None
                        };

                        SceneDependencyInfo sceneInfo =
                            ContentBuildInterface.CalculatePlayerDependenciesForScene(path, settings, usageTags);
                        dependencies = new string[sceneInfo.referencedObjects.Count];
                        for (int i = 0; i < sceneInfo.referencedObjects.Count; ++i)
                        {
                            if (string.IsNullOrEmpty(sceneInfo.referencedObjects[i].filePath))
                                dependencies[i] = AssetDatabase.GUIDToAssetPath(sceneInfo.referencedObjects[i].guid.ToString());
                            else
                                dependencies[i] = sceneInfo.referencedObjects[i].filePath;
                        }
                    }
                }
                else
                    dependencies = AssetDatabase.GetDependencies(path);

                if (!m_ResourcesToDependencies.ContainsKey(path))
                    m_ResourcesToDependencies.Add(path, new List<GUID>(dependencies.Length));
                else
                    m_ResourcesToDependencies[path].Capacity += dependencies.Length;

                foreach (string dependency in dependencies)
                {
                    if (dependency.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || dependency.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        continue;
                    m_ResourcesToDependencies[path].Add(new GUID(AssetDatabase.AssetPathToGUID(dependency)));
                }
            }

            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// Generate input definitions and entries for AssetBundleBuild
        /// </summary>
        /// <param name="catalog">The current Addressables catalog object</param>
        protected void CalculateInputDefinitions(AddressableCatalog catalog)
        {
            m_AllBundleInputDefs = catalog.GenerateBundleBuilds();
        }

        /// <summary>
        /// Build map of implicit guids to their bundle files
        /// </summary>
        /// <returns> Dictionary of implicit guids to their corresponding file</returns>
        protected internal Dictionary<GUID, List<string>> GetImplicitGuidToFilesMap()
        {
            if (m_ExtractData == null)
            {
                Debug.LogError("Build not run, RefreshBuild needed before GetImplicitGuidToFilesMap");
                return new Dictionary<GUID, List<string>>();
            }

            Dictionary<GUID, List<string>> implicitGuids = new Dictionary<GUID, List<string>>();
            IEnumerable<KeyValuePair<ObjectIdentifier, string>> validImplicitGuids =
                from fileToObject in m_ExtractData.WriteData.FileToObjects
                from objectId in fileToObject.Value
                where !m_ExtractData.WriteData.AssetToFiles.Keys.Contains(objectId.guid)
                select new KeyValuePair<ObjectIdentifier, string>(objectId, fileToObject.Key);

            //Build our Dictionary from our list of valid implicit guids (guids not already in explicit guids)
            foreach (var objectIdToFile in validImplicitGuids)
            {
                if (!implicitGuids.ContainsKey(objectIdToFile.Key.guid))
                    implicitGuids.Add(objectIdToFile.Key.guid, new List<string>());
                implicitGuids[objectIdToFile.Key.guid].Add(objectIdToFile.Value);
            }

            return implicitGuids;
        }

        /// <summary>
        /// Calculate built in resources and corresponding bundle dependencies
        /// </summary>
        /// <param name="catalog">The current Addressables catalog object</param>
        /// <param name="builtInResourcesPaths">Array of resource paths</param>
        /// <returns>List of rule results after calculating resource and bundle dependency combined</returns>
        protected List<AnalyzeResult> CalculateBuiltInResourceDependenciesToBundleDependecies(AddressableCatalog catalog, string[] builtInResourcesPaths)
        {
            List<AnalyzeResult> results = new List<AnalyzeResult>();

            if (!AddressablesUtils.CheckModifiedScenesAndAskToSave())
            {
                Debug.LogError("Cannot run Analyze with unsaved scenes");
                results.Add(new AnalyzeResult { resultName = ruleName + "Cannot run Analyze with unsaved scenes" });
                return results;
            }

            EditorUtility.DisplayProgressBar("Calculating Built-in dependencies", "Calculating dependencies between Built-in resources and Addressables", 0);
            try
            {
                // bulk of work and progress bars displayed in these methods
                var buildSuccess = BuildAndGetResourceDependencies(catalog, builtInResourcesPaths);
                if (buildSuccess != ReturnCode.Success)
                {
                    if (buildSuccess == ReturnCode.SuccessNotRun)
                    {
                        results.Add(new AnalyzeResult { resultName = ruleName + " - No issues found." });
                        return results;
                    }

                    results.Add(new AnalyzeResult { resultName = ruleName + "Analyze build failed. " + buildSuccess });
                    return results;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            results = (from resource in m_ResourcesToDependencies.Keys
                from dependency in m_ResourcesToDependencies[resource]
                let assetPath = AssetDatabase.GUIDToAssetPath(dependency.ToString())
                let files = m_ExtractData.WriteData.FileToObjects.Keys
                from file in files
                where m_ExtractData.WriteData.FileToObjects[file].Any(oid => oid.guid == dependency)
                where m_ExtractData.WriteData.FileToBundle.ContainsKey(file)
                let bundle = m_ExtractData.WriteData.FileToBundle[file]
                select new AnalyzeResult
                {
                    resultName =
                        resource + kDelimiter +
                        bundle + kDelimiter +
                        assetPath,
                    severity = MessageType.Warning
                }).ToList();

            if (results.Count == 0)
                results.Add(new AnalyzeResult { resultName = ruleName + " - No issues found." });

            return results;
        }

        /// <summary>
        /// Calculates and gathers dependencies for built in data.
        /// </summary>
        /// <param name="catalog">The AddressableAssetSettings to pull data from.</param>
        /// <param name="builtInResourcesPaths">The paths that lead to all the built in Resource locations</param>
        /// <returns>A ReturnCode indicating various levels of success or failure.</returns>
        protected ReturnCode BuildAndGetResourceDependencies(AddressableCatalog catalog, string[] builtInResourcesPaths)
        {
            BuiltInResourcesToDependenciesMap(builtInResourcesPaths);
            if (m_ResourcesToDependencies == null || m_ResourcesToDependencies.Count == 0)
                return ReturnCode.SuccessNotRun;

            CalculateInputDefinitions(catalog);
            if (m_AllBundleInputDefs == null)
                return ReturnCode.SuccessNotRun;

            EditorUtility.DisplayProgressBar("Calculating Built-in dependencies",
                "Calculating dependencies between Built-in resources and Addressables", 0.5f);

            ReturnCode exitCode = ReturnCode.Error;
            var context = new AddressableAssetsBuildContext(catalog);
            exitCode = RefreshBuild(context);
            if (exitCode < ReturnCode.Success)
            {
                EditorUtility.ClearProgressBar();
                return exitCode;
            }

            EditorUtility.DisplayProgressBar("Calculating Built-in dependencies",
                "Calculating dependencies between Built-in resources and Addressables", 0.9f);
            IntersectResourcesDepedenciesWithBundleDependencies(GetAllBundleDependencies());

            return exitCode;
        }

        /// <summary>
        /// Clear all previously gathered bundle data and analysis
        /// </summary>
        public override void ClearAnalysis()
        {
            m_AllBundleInputDefs = null;
            m_ResourcesToDependencies.Clear();
            m_ResultData = null;
            m_ExtractData = null;

            base.ClearAnalysis();
        }

        /// <summary>
        /// Data object for results of resource based analysis rules
        /// </summary>
        protected internal struct ResultData
        {
            public string ResourcePath;
            public string AssetBundleName;
            public string AssetPath;
        }

        private List<ResultData> m_ResultData = null;

        /// <summary>
        /// Duplicate Results between Addressables and Player content.
        /// </summary>
        protected IEnumerable<ResultData> Results
        {
            get
            {
                if (m_ResultData == null)
                {
                    if (ExtractData == null)
                    {
                        Debug.LogError("RefreshAnalysis needs to be called before getting results");
                        return new List<ResultData>(0);
                    }

                    m_ResultData = new List<ResultData>(512);

                    foreach (string resource in ResourcesToDependencies.Keys)
                    {
                        var dependencies = ResourcesToDependencies[resource];
                        foreach (GUID dependency in dependencies)
                        {
                            string assetPath = AssetDatabase.GUIDToAssetPath(dependency.ToString());
                            var files = ExtractData.WriteData.FileToObjects.Keys;
                            foreach (string file in files)
                            {
                                if (m_ExtractData.WriteData.FileToObjects[file].Any(oid => oid.guid == dependency) &&
                                    m_ExtractData.WriteData.FileToBundle.ContainsKey(file))
                                {
                                    string assetBundleName = ExtractData.WriteData.FileToBundle[file];
                                    m_ResultData.Add(new ResultData()
                                    {
                                        AssetBundleName = assetBundleName,
                                        AssetPath = assetPath,
                                        ResourcePath = resource
                                    });
                                }
                            }
                        }
                    }
                }

                return m_ResultData;
            }
        }

        /// <summary>
        /// Clear analysis and calculate built in content and corresponding bundle dependencies
        /// </summary>
        /// <param name="catalog">The current Addressables catalog object</param>
        /// <returns>List of results from analysis</returns>
        public override List<AnalyzeResult> RefreshAnalysis(AddressableCatalog catalog)
        {
            ClearAnalysis();
            List<AnalyzeResult> results = new List<AnalyzeResult>();

            if (!AddressablesUtils.CheckModifiedScenesAndAskToSave())
            {
                Debug.LogError("Cannot run Analyze with unsaved scenes");
                results.Add(new AnalyzeResult { resultName = ruleName + "Cannot run Analyze with unsaved scenes" });
                return results;
            }

            EditorUtility.DisplayProgressBar("Calculating Built-in dependencies", "Calculating dependencies between Resources and Addressables", 0);
            try
            {
                // bulk of work and progress bars displayed in these methods
                var resourcePaths = GetResourcePaths();

                var buildSuccess = BuildAndGetResourceDependencies(catalog, resourcePaths);
                if (buildSuccess == ReturnCode.SuccessNotRun)
                {
                    results.Add(new AnalyzeResult { resultName = ruleName + " - No issues found." });
                    return results;
                }

                if (buildSuccess != ReturnCode.Success)
                {
                    results.Add(new AnalyzeResult { resultName = ruleName + "Analyze build failed. " + buildSuccess });
                    return results;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            foreach (ResultData result in Results)
            {
                results.Add(new AnalyzeResult()
                {
                    resultName =
                        result.ResourcePath + kDelimiter +
                        result.AssetBundleName + kDelimiter +
                        result.AssetPath,
                    severity = MessageType.Warning
                });
            }

            return results;
        }

        /// <summary>
        /// Gets an array of resource paths that are to be compared against the addressables build content
        /// </summary>
        /// <returns>Array of Resource paths to compare against</returns>
        internal protected virtual string[] GetResourcePaths() => Array.Empty<string>();
    }
}