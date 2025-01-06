using UnityEngine;

namespace UnityEditor.AddressableAssets.GUI
{
    /// <summary>
    /// Window used to execute AnalyzeRule sets.
    /// </summary>
    public class AnalyzeWindow : EditorWindow
    {
        private static AnalyzeWindow s_Instance = null;

        private static AnalyzeWindow instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = GetWindow<AnalyzeWindow>(false, "Addressables Analyze", false);
                return s_Instance;
            }
        }

        private AddressableCatalog _m_Catalog;

        [SerializeField]
        private AnalyzeRuleGUI m_AnalyzeEditor;

        private Rect displayAreaRect => new(0, 0, position.width, position.height);

        [MenuItem("Window/Asset Management/Addressables/Analyze", priority = 2052)]
        internal static void ShowWindow()
        {
            AddressableCatalog catalog = AddressableCatalog.Default;
            if (catalog == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Attempting to open Addressables Analyze window, but no Addressables Settings file exists.  \n\nOpen 'Window/Asset Management/Addressables/Groups' for more info.", "Ok");
                return;
            }

            instance.titleContent = new GUIContent("Addressables Analyze");
            instance.Show();
        }

        private void OnEnable()
        {
            m_AnalyzeEditor ??= new AnalyzeRuleGUI();
        }

        private void OnGUI()
        {
            AddressableCatalog catalog = AddressableCatalog.Default;
            if (catalog == null)
                return;

            GUILayout.BeginArea(displayAreaRect);
            m_AnalyzeEditor.OnGUI(displayAreaRect);
            GUILayout.EndArea();
        }
    }
}
