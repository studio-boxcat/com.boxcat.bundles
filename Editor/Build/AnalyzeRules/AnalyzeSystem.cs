using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Bundles.Editor
{
    /// <summary>
    /// Static system to manage Analyze functionality.
    /// </summary>
    [Serializable]
    internal static class AnalyzeSystem
    {
        internal const string AnalyzeRuleDataFolder = PathConfig.LibraryPath + "/AnalyzeData";
        internal const string AnalyzeRuleDataName = "AnalyzeRuleData.json";
        internal const string AnalyzeRuleDataPath = AnalyzeRuleDataFolder + "/" + AnalyzeRuleDataName;

        internal static AssetCatalog Catalog => AssetCatalog.Default;

        private static AnalyzeRule[] m_Rules;
        internal static AnalyzeRule[] Rules
        {
            get
            {
                if (m_Rules is not null) return m_Rules;
                return m_Rules = new AnalyzeRule[]
                {
                    new CheckBundleDupeDependencies(),
                    new CheckSceneDupeDependencies(),
                    new CheckResourcesDupeDependencies(),
                    new BuildBundleLayout(),
                };
            }
        }

        [SerializeField]
        private static BundlesAnalyzeResultData m_AnalyzeData;

        internal static AssetSettingsAnalyzeTreeView TreeView { get; set; }

        internal static BundlesAnalyzeResultData AnalyzeData
        {
            get
            {
                if (m_AnalyzeData == null)
                {
                    if (!Directory.Exists(AnalyzeRuleDataFolder))
                        Directory.CreateDirectory(AnalyzeRuleDataFolder);
                    DeserializeData();
                }

                return m_AnalyzeData;
            }
        }

        internal static void ReloadUI()
        {
            TreeView?.Reload();
        }

        internal static void SerializeData()
        {
            SerializeData(AnalyzeRuleDataPath);
        }

        internal static void DeserializeData()
        {
            DeserializeData(AnalyzeRuleDataPath);
        }

        /// <summary>
        /// Serialize the analysis data to json and save to disk
        /// </summary>
        /// <param name="path">File path to save to</param>
        public static void SerializeData(string path)
        {
            File.WriteAllText(path, JsonUtility.ToJson(m_AnalyzeData));
        }

        /// <summary>
        /// Load and deserialize analysis data from json file and reload
        /// </summary>
        /// <param name="path">File path to load from</param>
        public static void DeserializeData(string path)
        {
            if (!File.Exists(path))
                File.WriteAllText(path, JsonUtility.ToJson(new BundlesAnalyzeResultData()));

            m_AnalyzeData = JsonUtility.FromJson<BundlesAnalyzeResultData>(File.ReadAllText(path));
            if (m_AnalyzeData == null)
                L.W($"Unable to load Analyze Result Data at {path}.");
            else
            {
                if (m_AnalyzeData.Data == null)
                    m_AnalyzeData.Data = new Dictionary<string, List<AnalyzeRule.AnalyzeResult>>();

                foreach (var rule in Rules)
                {
                    if (rule == null)
                    {
                        L.W("An unknown Analyze rule is being skipped because it is null.");
                        continue;
                    }

                    if (!m_AnalyzeData.Data.ContainsKey(rule.ruleName))
                        m_AnalyzeData.Data.Add(rule.ruleName, new List<AnalyzeRule.AnalyzeResult>());
                }
            }

            ReloadUI();
        }

        internal static List<AnalyzeRule.AnalyzeResult> RefreshAnalysis(AnalyzeRule rule)
        {
            if (rule == null)
                return null;

            if (!AnalyzeData.Data.ContainsKey(rule.ruleName))
                AnalyzeData.Data.Add(rule.ruleName, new List<AnalyzeRule.AnalyzeResult>());

            AnalyzeData.Data[rule.ruleName] = rule.RefreshAnalysis(Catalog);

            return AnalyzeData.Data[rule.ruleName];
        }

        internal static void ClearAnalysis(AnalyzeRule rule)
        {
            if (rule == null)
                return;

            if (!AnalyzeData.Data.ContainsKey(rule.ruleName))
                AnalyzeData.Data.Add(rule.ruleName, new List<AnalyzeRule.AnalyzeResult>());

            rule.ClearAnalysis();
            ;
            AnalyzeData.Data[rule.ruleName].Clear();
        }
    }
}
