using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    /// <summary>
    /// Rule class to check for duplicate bundle dependencies
    /// </summary>
    internal class CheckBundleDupeDependencies : BundleRuleBase
    {
        /// <summary>
        /// Result for checking for duplicates
        /// </summary>
        protected internal struct CheckDupeResult
        {
            public GroupKey Bundle;
            public string DuplicatedFile;
            public string AssetPath;
            public GUID DuplicatedGroupGuid;
        }

        /// <inheritdoc />
        public override string ruleName => "Check Duplicate Bundle Dependencies";

        [NonSerialized]
        internal readonly Dictionary<GroupKey, List<string>> m_AllIssues = new();

        [SerializeField]
        internal HashSet<GUID> m_ImplicitAssets;

        [NonSerialized]
        internal List<CheckDupeResult> m_ResultsData;

        /// <summary>
        /// Results calculated by the duplicate bundle dependencies check.
        /// </summary>
        protected IEnumerable<CheckDupeResult> CheckDupeResults
        {
            get
            {
                if (m_ResultsData == null)
                {
                    Debug.LogError("RefreshAnalysis needs to be called before getting results");
                    return new List<CheckDupeResult>(0);
                }

                return m_ResultsData;
            }
        }

        /// <summary>
        /// Clear current analysis and rerun check for duplicates
        /// </summary>
        /// <param name="catalog">The current Addressables catalog object</param>
        /// <returns>List of the analysis results</returns>
        public override List<AnalyzeResult> RefreshAnalysis(AddressableCatalog catalog)
        {
            ClearAnalysis();
            return CheckForDuplicateDependencies(catalog);
        }

        private void RefreshDisplay()
        {
            {
                m_Results = (from issueGroup in m_AllIssues
                    from item in issueGroup.Value
                    select new AnalyzeResult
                    {
                        resultName = issueGroup.Key.ToString() + kDelimiter + item,
                        severity = MessageType.Warning
                    }).ToList();
            }

            if (m_Results.Count == 0)
                m_Results.Add(noErrors);
        }

        /// <summary>
        /// Check for duplicates among the dependencies and build implicit duplicates
        /// </summary>
        /// <param name="catalog">The current Addressables catalog object</param>
        /// <returns>List of results from analysis</returns>
        protected List<AnalyzeResult> CheckForDuplicateDependencies(AddressableCatalog catalog)
        {
            if (!AddressablesUtils.CheckModifiedScenesAndAskToSave())
            {
                Debug.LogError("Cannot run Analyze with unsaved scenes");
                m_Results.Add(new AnalyzeResult {resultName = ruleName + "Cannot run Analyze with unsaved scenes"});
                return m_Results;
            }

            CalculateInputDefinitions(catalog);

            Assert.IsTrue(AllBundleInputDefs.Length is not 0,
                "No bundle input definitions found. Please check your Addressables settings.");

            var context = new AddressableAssetsBuildContext(catalog);
            ReturnCode exitCode = RefreshBuild(context);
            if (exitCode < ReturnCode.Success)
            {
                Debug.LogError("Analyze build failed. " + exitCode);
                m_Results.Add(new AnalyzeResult {resultName = ruleName + "Analyze build failed. " + exitCode});
                return m_Results;
            }

            var implicitGuids = GetImplicitGuidToFilesMap();
            var checkDupeResults = CalculateDuplicates(implicitGuids);
            BuildImplicitDuplicatedAssetsSet(checkDupeResults);
            m_ResultsData = checkDupeResults.ToList();

            RefreshDisplay();
            return m_Results;
        }

        /// <summary>
        /// Calculate duplicate dependencies
        /// </summary>
        /// <param name="implicitGuids">Map of implicit guids to their bundle files</param>
        /// <returns>Enumerable of results from duplicates check</returns>
        protected internal IEnumerable<CheckDupeResult> CalculateDuplicates(Dictionary<GUID, List<string>> implicitGuids)
        {
            //Get all guids that have more than one bundle referencing them
            IEnumerable<KeyValuePair<GUID, List<string>>> validGuids =
                from dupeGuid in implicitGuids
                where dupeGuid.Value.Distinct().Count() > 1
                select dupeGuid;

            return
                from guidToFile in validGuids
                from file in guidToFile.Value

                //Get the files that belong to those guids
                let bundle = (GroupKey) ExtractData.WriteData.FileToBundle[file]

                //Get the asset groups that belong to those bundles
                select new CheckDupeResult
                {
                    Bundle = bundle,
                    DuplicatedFile = file,
                    AssetPath = AssetDatabase.GUIDToAssetPath(guidToFile.Key.ToString()),
                    DuplicatedGroupGuid = guidToFile.Key
                };
        }

        internal void BuildImplicitDuplicatedAssetsSet(IEnumerable<CheckDupeResult> checkDupeResults)
        {
            m_ImplicitAssets = new HashSet<GUID>();

            foreach (var checkDupeResult in checkDupeResults)
            {
                if (!m_AllIssues.TryGetValue(checkDupeResult.Bundle, out var groupData))
                {
                    groupData = new List<string>();
                    m_AllIssues.Add(checkDupeResult.Bundle, groupData);
                }

                groupData.Add(checkDupeResult.AssetPath);
                m_ImplicitAssets.Add(checkDupeResult.DuplicatedGroupGuid);
            }
        }

        /// <inheritdoc />
        public override void ClearAnalysis()
        {
            m_AllIssues.Clear();
            m_ImplicitAssets = null;
            m_ResultsData = null;
            base.ClearAnalysis();
        }
    }
}