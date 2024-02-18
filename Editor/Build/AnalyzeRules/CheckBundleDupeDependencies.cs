using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    /// <summary>
    /// Rule class to check for duplicate bundle dependencies
    /// </summary>
    public class CheckBundleDupeDependencies : BundleRuleBase
    {
        /// <summary>
        /// Result for checking for duplicates
        /// </summary>
        protected internal struct CheckDupeResult
        {
            public BundleKey Bundle;
            public string DuplicatedFile;
            public string AssetPath;
            public GUID DuplicatedGroupGuid;
        }

        /// <summary>
        /// Results for duplicate results inverted check
        /// </summary>
        protected internal struct ExtraCheckBundleDupeData
        {
            public bool ResultsInverted;
        }

        /// <inheritdoc />
        public override string ruleName => "Check Duplicate Bundle Dependencies";

        [NonSerialized]
        internal readonly Dictionary<BundleKey, List<string>> m_AllIssues = new();

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
        /// <param name="settings">The current Addressables settings object</param>
        /// <returns>List of the analysis results</returns>
        public override List<AnalyzeResult> RefreshAnalysis(AddressableAssetSettings settings)
        {
            ClearAnalysis();
            return CheckForDuplicateDependencies(settings);
        }

        void RefreshDisplay()
        {
            var savedData = AnalyzeSystem.GetDataForRule<ExtraCheckBundleDupeData>(this);
            if (!savedData.ResultsInverted)
            {
                m_Results = (from issueGroup in m_AllIssues
                    from item in issueGroup.Value
                    select new AnalyzeResult
                    {
                        resultName = issueGroup.Key.ToString() + kDelimiter + item,
                        severity = MessageType.Warning
                    }).ToList();
            }
            else
            {
                m_Results = (from issueGroup in m_AllIssues
                    from item in issueGroup.Value
                    select new AnalyzeResult
                    {
                        resultName = item + kDelimiter + issueGroup.Key,
                        severity = MessageType.Warning
                    }).ToList();
            }

            if (m_Results.Count == 0)
                m_Results.Add(noErrors);
        }

        internal override IList<CustomContextMenu> GetCustomContextMenuItems()
        {
            IList<CustomContextMenu> customItems = new List<CustomContextMenu>();
            customItems.Add(new CustomContextMenu("Organize by Asset",
                () => InvertDisplay(),
                AnalyzeSystem.AnalyzeData.Data[ruleName].Any(),
                AnalyzeSystem.GetDataForRule<ExtraCheckBundleDupeData>(this).ResultsInverted));
            return customItems;
        }

        void InvertDisplay()
        {
            List<AnalyzeResult> updatedResults = new List<AnalyzeResult>();
            foreach (var result in AnalyzeSystem.AnalyzeData.Data[ruleName])
            {
                updatedResults.Add(new AnalyzeResult()
                {
                    //start at index 1 because the first result is going to be the rule name which we want to remain where it is.
                    resultName = ReverseStringFromIndex(result.resultName, 1, kDelimiter),
                    severity = result.severity
                });
            }

            AnalyzeSystem.ReplaceAnalyzeData(this, updatedResults);
            var savedData = AnalyzeSystem.GetDataForRule<ExtraCheckBundleDupeData>(this);
            savedData.ResultsInverted = !savedData.ResultsInverted;
            AnalyzeSystem.SaveDataForRule(this, savedData);
            AnalyzeSystem.SerializeData();
            AnalyzeSystem.ReloadUI();
        }

        private static string ReverseStringFromIndex(string data, int startingIndex, char delimiter)
        {
            string[] splitData = data.Split(delimiter);
            int i = startingIndex;
            int k = splitData.Length - 1;
            while (i < k)
            {
                (splitData[i], splitData[k]) = (splitData[k], splitData[i]);
                i++;
                k--;
            }

            return string.Join(kDelimiter.ToString(), splitData);
        }

        /// <summary>
        /// Check for duplicates among the dependencies and build implicit duplicates
        /// </summary>
        /// <param name="settings">The current Addressables settings object</param>
        /// <returns>List of results from analysis</returns>
        protected List<AnalyzeResult> CheckForDuplicateDependencies(AddressableAssetSettings settings)
        {
            if (!BuildUtility.CheckModifiedScenesAndAskToSave())
            {
                Debug.LogError("Cannot run Analyze with unsaved scenes");
                m_Results.Add(new AnalyzeResult {resultName = ruleName + "Cannot run Analyze with unsaved scenes"});
                return m_Results;
            }

            CalculateInputDefinitions(settings);

            if (AllBundleInputDefs.Count > 0)
            {
                var context = new AddressableAssetsBuildContext(settings);
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
            }
            else
            {
                m_ResultsData = new List<CheckDupeResult>(0);
                m_ImplicitAssets = new HashSet<GUID>();
            }

            AddressableAnalytics.ReportUsageEvent(AddressableAnalytics.UsageEventType.RunCheckBundleDupeDependenciesRule);
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
                where IsValidPath(AssetDatabase.GUIDToAssetPath(dupeGuid.Key.ToString()))
                select dupeGuid;

            return
                from guidToFile in validGuids
                from file in guidToFile.Value

                //Get the files that belong to those guids
                let bundle = BundleKey.FromBuildName(ExtractData.WriteData.FileToBundle[file])

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

    [InitializeOnLoad]
    class RegisterCheckBundleDupeDependencies
    {
        static RegisterCheckBundleDupeDependencies()
        {
            AnalyzeSystem.RegisterNewRule<CheckBundleDupeDependencies>();
        }
    }
}