using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    class BuildBundleLayout : BundleRuleBase
    {
        /// <summary>
        /// Result data for assets included in the bundle layout
        /// </summary>
        protected struct BuildBundleLayoutResultData
        {
            public string AssetBundleName;
            public List<string> Dependencies;
            public List<string> Explicits;
            public List<string> Implicits;
        }

        /// <inheritdoc />
        public override string ruleName => "Bundle Layout Preview";

        private List<BuildBundleLayoutResultData> m_ResultData = null;

        /// <summary>
        /// Results of the build Layout.
        /// </summary>
        protected IEnumerable<BuildBundleLayoutResultData> BuildBundleLayoutResults
            => m_ResultData ??= BuildResult(AllBundleInputDefs, ExtractData.WriteData);

        /// <inheritdoc />
        public override List<AnalyzeResult> RefreshAnalysis(AddressableCatalog catalog)
        {
            ClearAnalysis();

            if (!BuildUtility.CheckModifiedScenesAndAskToSave())
            {
                Debug.LogError("Cannot run Analyze with unsaved scenes");
                m_Results.Add(new AnalyzeResult(ruleName + "Cannot run Analyze with unsaved scenes"));
                return m_Results;
            }

            CalculateInputDefinitions(catalog);
            var context = new AddressableAssetsBuildContext(catalog);
            RefreshBuild(context);
            foreach (var resultData in BuildBundleLayoutResults)
            {
                var prefix = resultData.AssetBundleName + kDelimiter + "Explicit" + kDelimiter;
                foreach (var path in resultData.Explicits)
                    m_Results.Add(new AnalyzeResult(prefix + path));
                prefix = resultData.AssetBundleName + kDelimiter + "Implicit" + kDelimiter;
                foreach (var path in resultData.Implicits)
                    m_Results.Add(new AnalyzeResult(prefix + path));
            }

            if (m_Results.Count == 0)
                m_Results.Add(noErrors);
            return m_Results;
        }

        /// <inheritdoc />
        public override void ClearAnalysis()
        {
            m_ResultData = null;
            base.ClearAnalysis();
        }

        [InitializeOnLoad]
        class RegisterBuildBundleLayout
        {
            static RegisterBuildBundleLayout()
            {
                AnalyzeSystem.RegisterNewRule<BuildBundleLayout>();
            }
        }

        static List<BuildBundleLayoutResultData> BuildResult(
            List<AssetBundleBuild> buildInput, IBundleWriteData buildOutput)
        {
            var result = new List<BuildBundleLayoutResultData>(buildInput.Count);

            var bundleToFile = buildOutput.FileToBundle
                .ToDictionary(x => x.Value, x => x.Key);

            foreach (var bundleBuild in buildInput)
            {
                var bundleName = bundleBuild.assetBundleName;
                var explicitAssets = new List<string>(bundleBuild.assetNames);
                var implicitAssets = buildOutput.FileToObjects[bundleToFile[bundleName]]
                    .Where(x => !buildOutput.AssetToFiles.ContainsKey(x.guid)) // Exclude explicit assets
                    .Select(x => AssetDatabase.GUIDToAssetPath(x.guid))
                    .ToList();

                result.Add(new BuildBundleLayoutResultData()
                {
                    AssetBundleName = bundleName,
                    Explicits = explicitAssets,
                    Implicits = implicitAssets,
                });
            }

            return result;
        }
    }
}