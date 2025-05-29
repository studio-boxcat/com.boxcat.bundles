using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Bundles.Editor
{
    /// <summary>
    /// Window used to execute AnalyzeRule sets.
    /// </summary>
    public class AnalyzeWindow : EditorWindow
    {
        [SerializeField]
        private AnalyzeRuleGUI m_AnalyzeEditor;

        private Rect displayAreaRect => new(0, 0, position.width, position.height);

        [Shortcut("Addressables/Analyze")]
        internal static void ShowWindow()
        {
            GetWindow<AnalyzeWindow>("Addressables Analyze", desiredDockNextTo: typeof(SceneView)).Focus();
        }

        private void OnEnable()
        {
            m_AnalyzeEditor ??= new AnalyzeRuleGUI();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(displayAreaRect);
            m_AnalyzeEditor.OnGUI(displayAreaRect);
            GUILayout.EndArea();
        }
    }
}