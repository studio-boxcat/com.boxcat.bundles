using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Bundles.Editor
{
    /// <summary>
    /// Base class for creating rules to analyze Addressables data.  Use AnalyzeWindow.RegisterNewRule&lt;T&gt;() to register.
    ///  a rule with the GUI window.
    /// </summary>
    [Serializable]
    internal class AnalyzeRule
    {
        [SerializeField]
        internal List<AnalyzeResult> m_Results = new List<AnalyzeResult>();

        /// <summary>
        /// Represents a state where no errors were found after analyzing Addressables data.
        /// </summary>
        [NonSerialized]
        protected AnalyzeResult noErrors = new AnalyzeResult {resultName = "No issues found"};

        /// <summary>
        /// Delimiter character used in analyze rule string names.  This is used when a rule result needs to display
        /// as a tree view hierarchy.  A rule result of A:B:C will end up in the tree view with:
        ///  - A
        ///  --- B
        ///  ----- C
        /// </summary>
        public const char kDelimiter = ':';

        /// <summary>
        /// Result data returned by rules.
        /// </summary>
        [Serializable]
        public class AnalyzeResult
        {
            [SerializeField]
            private string m_ResultName;

            /// <summary>
            /// Name of result data.  This name uses AnalyzeRule.kDelimiter to signify breaks in the tree display.
            /// </summary>
            public string resultName
            {
                get => m_ResultName;
                set => m_ResultName = value;
            }

            [SerializeField]
            private MessageType m_Severity = MessageType.None;

            /// <summary>
            /// Severity of rule result
            /// </summary>
            public MessageType severity
            {
                get => m_Severity;
                set => m_Severity = value;
            }

            public AnalyzeResult()
            {
            }

            public AnalyzeResult(string resultName)
            {
                m_ResultName = resultName;
            }
        }

        /// <summary>
        /// Display name for rule
        /// </summary>
        public virtual string ruleName => GetType().ToString();

        /// <summary>
        /// This method runs the actual analysis for the rule.
        /// </summary>
        /// <param name="catalog">The catalog object to analyze</param>
        /// <returns>A list of resulting information (warnings, errors, or info)</returns>
        public virtual List<AnalyzeResult> RefreshAnalysis(AddressableCatalog catalog)
        {
            return new List<AnalyzeResult>();
        }

        /// <summary>
        /// Clears out the analysis results. When overriding, use to clear rule-specific data as well.
        /// </summary>
        public virtual void ClearAnalysis()
        {
            m_Results.Clear();
        }
    }
}
