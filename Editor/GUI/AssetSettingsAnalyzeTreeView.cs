using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets.GUI
{
    internal class AssetSettingsAnalyzeTreeView : TreeView
    {
        private int m_CurrentDepth;

        internal AssetSettingsAnalyzeTreeView(TreeViewState state)
            : base(state)
        {
            showAlternatingRowBackgrounds = true;
            showBorder = true;

            Reload();
        }

        private IEnumerable<AnalyzeRuleContainerTreeViewItem> AllRuleContainers()
        {
            return rootItem.children
                .OfType<AnalyzeRuleContainerTreeViewItem>();
        }

        public void RunEntireRules()
        {
            foreach (var ruleContainer in AllRuleContainers())
            {
                var results = AnalyzeSystem.RefreshAnalysis(ruleContainer.analyzeRule);
                BuildResults(ruleContainer, results);
            }
            Reload();
        }

        public void ClearAll()
        {
            foreach (var ruleContainer in AllRuleContainers())
            {
                AnalyzeSystem.ClearAnalysis(ruleContainer.analyzeRule);
                BuildResults(ruleContainer, new List<AnalyzeRule.AnalyzeResult>());
            }
            Reload();
        }

        public override void OnGUI(Rect rect)
        {
            base.OnGUI(rect);

            var e = Event.current;
            if (e.UseKey(KeyCode.Return))
            {
                var selection = GetSelection()
                    .Select(x => FindItem(x, rootItem) as AnalyzeResultsTreeViewItem)
                    .Where(x => x != null)
                    .SelectMany(x => x.results)
                    .Select(x => x.resultName.SplitLast(AnalyzeRule.kDelimiter))
                    .Distinct()
                    .Select(AssetDatabase.LoadMainAssetAtPath)
                    .Where(x => x)
                    .ToArray();

                if (selection.Length is not 0)
                {
                    selection = Selection.objects;
                    EditorGUIUtility.PingObject(selection[0]);
                }
            }
        }

        protected override void DoubleClickedItem(int id)
        {
            var item = FindItem(id, rootItem) as AnalyzeResultsTreeViewItem;
            if (item != null)
                item.DoubleClicked();
        }

        protected override TreeViewItem BuildRoot()
        {
            m_CurrentDepth = 0;
            var root = new TreeViewItem(-1, -1);
            root.children = new List<TreeViewItem>();

            AnalyzeSystem.TreeView = this;

            var ruleContainers = new List<AnalyzeRuleContainerTreeViewItem>();
            foreach (var rule in AnalyzeSystem.Rules)
            {
                var ruleContainer = new AnalyzeRuleContainerTreeViewItem(
                    rule.ruleName.GetHashCode(), m_CurrentDepth, rule);
                root.AddChild(ruleContainer);
                ruleContainers.Add(ruleContainer);
            }

            m_CurrentDepth++;

            var index = 0;
            foreach (var ruleContainer in ruleContainers)
            {
                if (ruleContainer == null)
                    continue;

                EditorUtility.DisplayProgressBar("Calculating Analyze Results...", ruleContainer.displayName, (index / (float) ruleContainers.Count));
                if (AnalyzeSystem.AnalyzeData.Data.ContainsKey(ruleContainer.analyzeRule.ruleName))
                    BuildResults(ruleContainer, AnalyzeSystem.AnalyzeData.Data[ruleContainer.analyzeRule.ruleName]);

                index++;
            }

            EditorUtility.ClearProgressBar();
            return root;
        }

        private readonly Dictionary<int, AnalyzeResultsTreeViewItem> hashToAnalyzeResults = new Dictionary<int, AnalyzeResultsTreeViewItem>();

        private void BuildResults(TreeViewItem root, List<AnalyzeRule.AnalyzeResult> ruleResults)
        {
            hashToAnalyzeResults.Clear();
            int updateFrequency = Mathf.Max(ruleResults.Count / 10, 1);

            for (int index = 0; index < ruleResults.Count; ++index)
            {
                var result = ruleResults[index];
                if (index == 0 || index % updateFrequency == 0)
                    EditorUtility.DisplayProgressBar("Building Results Tree...", result.resultName, (float) index / hashToAnalyzeResults.Keys.Count);

                var resPath = result.resultName.Split(AnalyzeRule.kDelimiter);
                string name = string.Empty;
                TreeViewItem parent = root;

                for (int i = 0; i < resPath.Length; i++)
                {
                    name += resPath[i];
                    int hash = name.GetHashCode();

                    if (!hashToAnalyzeResults.ContainsKey(hash))
                    {
                        AnalyzeResultsTreeViewItem item = new AnalyzeResultsTreeViewItem(hash, i + m_CurrentDepth, resPath[i], result.severity, result);
                        hashToAnalyzeResults.Add(item.id, item);
                        parent.AddChild(item);
                        parent = item;
                    }
                    else
                    {
                        var targetItem = hashToAnalyzeResults[hash];
                        targetItem.results.Add(result);
                        parent = targetItem;
                    }
                }
            }

            EditorUtility.ClearProgressBar();

            List<TreeViewItem> allTreeViewItems = new List<TreeViewItem>();
            allTreeViewItems.Add(root);
            allTreeViewItems.AddRange(root.children);

            foreach (var node in allTreeViewItems)
                (node as AnalyzeTreeViewItemBase)?.AddIssueCountToName();

            AnalyzeSystem.SerializeData();
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item as AnalyzeResultsTreeViewItem;
            if (item != null && item.severity != MessageType.None)
            {
                Texture2D icon = null;
                switch (item.severity)
                {
                    case MessageType.Info:
                        icon = GetInfoIcon();
                        break;
                    case MessageType.Warning:
                        icon = GetWarningIcon();
                        break;
                    case MessageType.Error:
                        icon = GetErrorIcon();
                        break;
                }

                UnityEngine.GUI.Label(
                    new Rect(args.rowRect.x + baseIndent, args.rowRect.y, args.rowRect.width - baseIndent,
                        args.rowRect.height), new GUIContent(icon, string.Empty));
            }

            base.RowGUI(args);
        }

        private Texture2D m_ErrorIcon;
        private Texture2D m_WarningIcon;
        private Texture2D m_InfoIcon;

        private Texture2D GetErrorIcon()
        {
            if (m_ErrorIcon == null)
                FindMessageIcons();
            return m_ErrorIcon;
        }

        private Texture2D GetWarningIcon()
        {
            if (m_WarningIcon == null)
                FindMessageIcons();
            return m_WarningIcon;
        }

        private Texture2D GetInfoIcon()
        {
            if (m_InfoIcon == null)
                FindMessageIcons();
            return m_InfoIcon;
        }

        private void FindMessageIcons()
        {
            m_ErrorIcon = EditorGUIUtility.FindTexture("console.errorIcon");
            m_WarningIcon = EditorGUIUtility.FindTexture("console.warnicon");
            m_InfoIcon = EditorGUIUtility.FindTexture("console.infoIcon");
        }
    }

    internal class AnalyzeTreeViewItemBase : TreeViewItem
    {
        private string baseDisplayName;
        private string currentDisplayName;

        public override string displayName
        {
            get { return currentDisplayName; }
            set { baseDisplayName = value; }
        }

        public AnalyzeTreeViewItemBase(int id, int depth, string displayName) : base(id, depth,
            displayName)
        {
            currentDisplayName = baseDisplayName = displayName;
        }

        public int AddIssueCountToName()
        {
            int issueCount = 0;
            if (children != null)
            {
                foreach (var child in children)
                {
                    var analyzeNode = child as AnalyzeResultsTreeViewItem;
                    if (analyzeNode != null)
                        issueCount += analyzeNode.AddIssueCountToName();
                }
            }

            if (issueCount == 0)
                return 1;

            currentDisplayName = baseDisplayName + " (" + issueCount + ")";
            return issueCount;
        }
    }

    internal class AnalyzeResultsTreeViewItem : AnalyzeTreeViewItemBase
    {
        public MessageType severity { get; set; }
        public HashSet<AnalyzeRule.AnalyzeResult> results { get; }

        public AnalyzeResultsTreeViewItem(int id, int depth, string displayName, MessageType type, AnalyzeRule.AnalyzeResult analyzeResult)
            : base(id, depth, displayName)
        {
            severity = type;
            results = new HashSet<AnalyzeRule.AnalyzeResult>() {analyzeResult};
        }

        private static UnityEngine.Object GetResultObject(string resultName)
        {
            int li = resultName.LastIndexOf(AnalyzeRule.kDelimiter);
            if (li >= 0)
            {
                string assetPath = resultName.Substring(li + 1);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(guid))
                    return AssetDatabase.LoadMainAssetAtPath(assetPath);
            }

            return null;
        }

        internal void DoubleClicked()
        {
            HashSet<UnityEngine.Object> objects = new HashSet<Object>();
            foreach (var itemResult in results)
            {
                Object o = GetResultObject(itemResult.resultName);
                if (o != null)
                    objects.Add(o);
            }

            if (objects.Count > 0)
            {
                // Selection.objects = objects.ToArray();
                foreach (Object o in objects)
                    EditorGUIUtility.PingObject(o);
            }
        }
    }

    internal class AnalyzeRuleContainerTreeViewItem : AnalyzeTreeViewItemBase
    {
        internal AnalyzeRule analyzeRule;

        public AnalyzeRuleContainerTreeViewItem(int id, int depth, AnalyzeRule rule) : base(id, depth, rule.ruleName)
        {
            analyzeRule = rule;
        }
    }
}