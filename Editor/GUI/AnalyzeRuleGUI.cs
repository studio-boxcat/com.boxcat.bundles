using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Bundles.Editor
{
    [Serializable]
    internal class AnalyzeRuleGUI
    {
        [SerializeField]
        private TreeViewState m_TreeState;

        private AssetSettingsAnalyzeTreeView m_Tree;

        private const float k_ButtonHeight = 20f;

        private GUIContent m_AnalyzeGUIContent = new GUIContent("Analyze");
        private GUIContent m_ClearGUIContent = new GUIContent("Clear");

        private GUIContent m_ExportJsonGUIContent = new GUIContent("Export Results", "Export a json file with the analyze results for all rules");
        private GUIContent m_ImportJsonGUIContent = new GUIContent("Import Results", "Import a json file with the analyze results for all rules, this will overwrite any existing results");

        internal void OnGUI(Rect rect)
        {
            if (m_Tree == null)
            {
                if (m_TreeState == null)
                    m_TreeState = new TreeViewState();

                m_Tree = new AssetSettingsAnalyzeTreeView(m_TreeState);
                m_Tree.Reload();
            }

            var treeRect = new Rect(rect.xMin, rect.yMin + k_ButtonHeight, rect.width, rect.height - k_ButtonHeight);
            m_Tree.OnGUI(treeRect);

            var buttonRect = new Rect(rect.xMin, rect.yMin, rect.width, rect.height);
            buttonRect.height = k_ButtonHeight;

            GUILayout.BeginArea(buttonRect);

            var runRect = buttonRect;
            float activeWidth = 80;
            runRect.width = activeWidth;
            buttonRect.x += activeWidth;
            buttonRect.width -= activeWidth;
            if (UnityEngine.GUI.Button(runRect, m_AnalyzeGUIContent, EditorStyles.toolbarButton))
            {
                EditorApplication.delayCall += () => m_Tree.RunEntireRules();
            }

            var clearRect = buttonRect;
            activeWidth = 80;
            clearRect.width = activeWidth;
            buttonRect.x += activeWidth;
            buttonRect.width -= activeWidth;
            if (UnityEngine.GUI.Button(clearRect, m_ClearGUIContent, EditorStyles.toolbarButton))
            {
                EditorApplication.delayCall += () => m_Tree.ClearAll();
            }

            GUIStyle m_ToolbarButtonStyle = "RL FooterButton";
            GUIContent m_ManageLabelsButtonContent = EditorGUIUtility.TrIconContent("_Popup@2x", "Import/Export Analysis Results");
            Rect plusRect = buttonRect;
            plusRect.height = k_ButtonHeight;
            plusRect.width = plusRect.height;
            plusRect.x = (buttonRect.width - plusRect.width) + buttonRect.x;
            if (plusRect.x < buttonRect.x)
                plusRect.x = buttonRect.x;
            plusRect.y += 2;
            if (UnityEngine.GUI.Button(plusRect, m_ManageLabelsButtonContent, m_ToolbarButtonStyle))
            {
                var menu = new GenericMenu();
                menu.AddItem(m_ExportJsonGUIContent, false, () => EditorApplication.delayCall += () =>
                {
                    // select to save dialog
                    var path = EditorUtility.SaveFilePanel("Export analysis results to json", "",
                        "BundlesAnalyseResults", "json");
                    AnalyzeSystem.SerializeData(path);
                });
                menu.AddItem(m_ImportJsonGUIContent, false, () => EditorApplication.delayCall += () =>
                {
                    var path = EditorUtility.OpenFilePanel("Import analysis results from json", "", "json");
                    if (!string.IsNullOrEmpty(path))
                        AnalyzeSystem.DeserializeData(path);
                });
                menu.DropDown(plusRect);
            }

            GUILayout.EndArea();
        }
    }
}
