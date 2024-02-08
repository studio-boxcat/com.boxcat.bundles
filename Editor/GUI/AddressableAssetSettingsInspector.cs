using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets.GUI
{
    [ExcludeFromCodeCoverage]
    [CustomEditor(typeof(AddressableAssetSettings))]
    class AddressableAssetSettingsInspector : Editor
    {
        AddressableAssetSettings m_AasTarget;

        static FoldoutSessionStateValue BuildFoldout = new FoldoutSessionStateValue("Addressables.BuildFoldout");
        GUIContent m_BuildHeader;
        static FoldoutSessionStateValue DataBuildersFoldout = new FoldoutSessionStateValue("Addressables.DataBuildersFoldout");
        GUIContent m_DataBuildersHeader;
        static FoldoutSessionStateValue GroupTemplateObjectsFoldout = new FoldoutSessionStateValue("Addressables.GroupTemplateObjectsFoldout");
        GUIContent m_GroupTemplateObjectsHeader;

        //Used for displaying path pairs
        bool m_UseCustomPaths = false;
        bool m_ShowPaths = true;
        bool m_ShowContentStatePath = true;

        [FormerlySerializedAs("m_dataBuildersRL")]
        [SerializeField]
        ReorderableList m_DataBuildersRl;

        [SerializeField]
        ReorderableList m_GroupTemplateObjectsRl;

        List<Action> m_QueuedChanges = new List<Action>();

        void OnEnable()
        {
            m_AasTarget = target as AddressableAssetSettings;
            if (m_AasTarget == null)
                return;

            m_DataBuildersRl = new ReorderableList(m_AasTarget.DataBuilders, typeof(ScriptableObject), true, true, true, true);
            m_DataBuildersRl.drawElementCallback = DrawDataBuilderCallback;
            m_DataBuildersRl.headerHeight = 0;
            m_DataBuildersRl.onAddDropdownCallback = OnAddDataBuilder;
            m_DataBuildersRl.onRemoveCallback = OnRemoveDataBuilder;

            m_GroupTemplateObjectsRl = new ReorderableList(m_AasTarget.GroupTemplateObjects, typeof(ScriptableObject), true, true, true, true);
            m_GroupTemplateObjectsRl.drawElementCallback = DrawGroupTemplateObjectCallback;
            m_GroupTemplateObjectsRl.headerHeight = 0;
            m_GroupTemplateObjectsRl.onAddDropdownCallback = OnAddGroupTemplateObject;
            m_GroupTemplateObjectsRl.onRemoveCallback = OnRemoveGroupTemplateObject;

            m_BuildHeader = new GUIContent("Build", "Settings affect profiles.");

            m_DataBuildersHeader = new GUIContent("Build and Play Mode Scripts", "Settings affect profiles.");
            m_GroupTemplateObjectsHeader = new GUIContent("Asset Group Templates", "Settings affect profiles.");
        }

        GUIContent m_ManageGroups =
            new GUIContent("Manage Groups", "Open the Addressables Groups window");

        GUIContent m_ManageProfiles =
            new GUIContent("Manage Profiles", "Open the Addressables Profiles window");

        GUIContent m_UniqueBundles =
            new GUIContent("Unique Bundle IDs",
                "If set, every content build (original or update) will result in asset bundles with more complex internal names.  This may result in more bundles being rebuilt, but safer mid-run updates.  See docs for more info.");

        GUIContent m_ContiguousBundles =
            new GUIContent("Contiguous Bundles",
                "If set, packs assets in bundles contiguously based on the ordering of the source asset which results in improved asset loading times. Disable this if you've built bundles with a version of Addressables older than 1.12.1 and you want to minimize bundle changes.");
#if NONRECURSIVE_DEPENDENCY_DATA
        GUIContent m_NonRecursiveBundleBuilding =
            new GUIContent("Non-Recursive Dependency Calculation",
                "If set, Calculates and build asset bundles using Non-Recursive Dependency calculation methods. This approach helps reduce asset bundle rebuilds and runtime memory consumption.");
#else
        GUIContent m_NonRecursiveBundleBuilding =
            new GUIContent("Non-Recursive Dependency Calculation", "If set, Calculates and build asset bundles using Non-Recursive Dependency calculation methods. This approach helps reduce asset bundle rebuilds and runtime memory consumption.\n*Requires Unity 2019.4.19f1 or above");
#endif

        GUIContent m_IgnoreUnsupportedFilesInBuild =
            new GUIContent("Ignore Invalid/Unsupported Files in Build", "If enabled, files that cannot be built will be ignored.");

        GUIContent m_ContentStateFileBuildPath =
            new GUIContent("Content State Build Path", "The path used for the addressables_content_state.bin file, which is used to detect modified assets during a Content Update.");

        GUIContent m_ShaderBundleNaming =
            new GUIContent("Shader Bundle Naming Prefix",
                "This setting determines how the Unity built in shader bundle will be named during the build.  The recommended setting is Project Name Hash.");

        GUIContent m_ShaderBundleCustomNaming =
            new GUIContent("Shader Bundle Custom Prefix", "Custom prefix for Unity built in shader bundle.");

        GUIContent m_MonoBundleNaming =
            new GUIContent("MonoScript Bundle Naming Prefix",
                "This setting determines how and if the MonoScript bundle will be named during the build.  The recommended setting is Project Name Hash.");

        GUIContent m_MonoBundleCustomNaming =
            new GUIContent("MonoScript Bundle Custom Prefix", "Custom prefix for MonoScript bundle.");

        GUIContent m_StripUnityVersionFromBundleBuild =
            new GUIContent("Strip Unity Version from AssetBundles", "If enabled, the Unity Editor version is stripped from the archive file header and the bundles's serialized files.");

        GUIContent m_DisableVisibleSubAssetRepresentations =
            new GUIContent("Disable Visible Sub Asset Representations", "If enabled, the build will assume that all sub Assets have no visible asset representations.");

#if (ENABLE_CCD)
        GUIContent m_BuildAndReleaseBinFile =
            new GUIContent("For Build & Release", "Determines where the system attempts to pull the previous content state file from for the Content Update.");
#endif

#if UNITY_2021_2_OR_NEWER
        GUIContent m_BuildAddressablesWithPlayerBuild =
            new GUIContent("Build Addressables on Player Build", "Determines if a new Addressables build will be built with a Player Build.");

        GUIContent[] m_BuildAddressablesWithPlayerBuildOptions = new GUIContent[]
        {
            new GUIContent("Use global Settings (stored in preferences)", "Use settings specified in the Preferences window"),
            new GUIContent("Build Addressables content on Player Build", "A new Addressables build will be created with a Player Build"),
            new GUIContent("Do not Build Addressables content on Player build", "No new Addressables build will be created with a Player Build")
        };
#endif

        public override bool RequiresConstantRepaint()
        {
            return true;
        }

        public override void OnInspectorGUI()
        {
            m_QueuedChanges.Clear();
            serializedObject.UpdateIfRequiredOrScript(); // use updated values
            EditorGUI.BeginChangeCheck();
            float postBlockContentSpace = 10;

            GUILayout.Space(8);
            if (GUILayout.Button(m_ManageGroups, "Minibutton", GUILayout.ExpandWidth(true)))
            {
                AddressableAssetsWindow.Init();
            }

            BuildFoldout.IsActive = AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(BuildFoldout.IsActive, m_BuildHeader, () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("editor/AddressableAssetSettings.html#build");
                Application.OpenURL(url);
            });
            if (BuildFoldout.IsActive)
            {
#if UNITY_2021_2_OR_NEWER
                int index = (int)m_AasTarget.BuildAddressablesWithPlayerBuild;
                int newIndex = EditorGUILayout.Popup(m_BuildAddressablesWithPlayerBuild, index, m_BuildAddressablesWithPlayerBuildOptions);
                if (index != newIndex)
                    m_QueuedChanges.Add(() => m_AasTarget.BuildAddressablesWithPlayerBuild = (AddressableAssetSettings.PlayerBuildOption)newIndex);
                if (newIndex == 0)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        bool enabled = EditorPrefs.GetBool(AddressablesPreferences.kBuildAddressablesWithPlayerBuildKey, true);
                        EditorGUILayout.TextField(" ", enabled ? "Enabled" : "Disabled");
                    }
                }
#endif

                bool ignoreUnsupportedFilesInBuild = EditorGUILayout.Toggle(m_IgnoreUnsupportedFilesInBuild, m_AasTarget.IgnoreUnsupportedFilesInBuild);
                if (ignoreUnsupportedFilesInBuild != m_AasTarget.IgnoreUnsupportedFilesInBuild)
                    m_QueuedChanges.Add(() => m_AasTarget.IgnoreUnsupportedFilesInBuild = ignoreUnsupportedFilesInBuild);

                bool uniqueBundleIds = EditorGUILayout.Toggle(m_UniqueBundles, m_AasTarget.UniqueBundleIds);
                if (uniqueBundleIds != m_AasTarget.UniqueBundleIds)
                    m_QueuedChanges.Add(() => m_AasTarget.UniqueBundleIds = uniqueBundleIds);

                bool contiguousBundles = EditorGUILayout.Toggle(m_ContiguousBundles, m_AasTarget.ContiguousBundles);
                if (contiguousBundles != m_AasTarget.ContiguousBundles)
                    m_QueuedChanges.Add(() => m_AasTarget.ContiguousBundles = contiguousBundles);

#if !NONRECURSIVE_DEPENDENCY_DATA
                EditorGUI.BeginDisabledGroup(true);
#endif
                bool nonRecursiveBuilding = EditorGUILayout.Toggle(m_NonRecursiveBundleBuilding, m_AasTarget.NonRecursiveBuilding);
                if (nonRecursiveBuilding != m_AasTarget.NonRecursiveBuilding)
                    m_QueuedChanges.Add(() => m_AasTarget.NonRecursiveBuilding = nonRecursiveBuilding);
#if !NONRECURSIVE_DEPENDENCY_DATA
                EditorGUI.EndDisabledGroup();
#endif

                ShaderBundleNaming shaderBundleNaming = (ShaderBundleNaming)EditorGUILayout.Popup(m_ShaderBundleNaming,
                    (int)m_AasTarget.ShaderBundleNaming, new[] {"Project Name Hash", "Default Group GUID", "Custom"});
                if (shaderBundleNaming != m_AasTarget.ShaderBundleNaming)
                    m_QueuedChanges.Add(() => m_AasTarget.ShaderBundleNaming = shaderBundleNaming);
                if (shaderBundleNaming == ShaderBundleNaming.Custom)
                {
                    string customShaderBundleName = EditorGUILayout.TextField(m_ShaderBundleCustomNaming, m_AasTarget.ShaderBundleCustomNaming);
                    if (customShaderBundleName != m_AasTarget.ShaderBundleCustomNaming)
                        m_QueuedChanges.Add(() => m_AasTarget.ShaderBundleCustomNaming = customShaderBundleName);
                }

                MonoScriptBundleNaming monoBundleNaming = (MonoScriptBundleNaming)EditorGUILayout.Popup(m_MonoBundleNaming,
                    (int)m_AasTarget.MonoScriptBundleNaming, new[] {"Disable MonoScript Bundle Build", "Project Name Hash", "Default Group GUID", "Custom"});
                if (monoBundleNaming != m_AasTarget.MonoScriptBundleNaming)
                    m_QueuedChanges.Add(() => m_AasTarget.MonoScriptBundleNaming = monoBundleNaming);
                if (monoBundleNaming == MonoScriptBundleNaming.Custom)
                {
                    string customMonoScriptBundleName = EditorGUILayout.TextField(m_MonoBundleCustomNaming, m_AasTarget.MonoScriptBundleCustomNaming);
                    if (customMonoScriptBundleName != m_AasTarget.MonoScriptBundleCustomNaming)
                        m_QueuedChanges.Add(() => m_AasTarget.MonoScriptBundleCustomNaming = customMonoScriptBundleName);
                }

                bool stripUnityVersion = EditorGUILayout.Toggle(m_StripUnityVersionFromBundleBuild, m_AasTarget.StripUnityVersionFromBundleBuild);
                if (stripUnityVersion != m_AasTarget.StripUnityVersionFromBundleBuild)
                    m_QueuedChanges.Add(() => m_AasTarget.StripUnityVersionFromBundleBuild = stripUnityVersion);

                bool disableVisibleSubAssetRepresentations = EditorGUILayout.Toggle(m_DisableVisibleSubAssetRepresentations, m_AasTarget.DisableVisibleSubAssetRepresentations);
                if (disableVisibleSubAssetRepresentations != m_AasTarget.DisableVisibleSubAssetRepresentations)
                    m_QueuedChanges.Add(() => m_AasTarget.DisableVisibleSubAssetRepresentations = disableVisibleSubAssetRepresentations);
                GUILayout.Space(postBlockContentSpace);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            DataBuildersFoldout.IsActive = AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(DataBuildersFoldout.IsActive, m_DataBuildersHeader, () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("editor/AddressableAssetSettings.html#build-and-play-mode-scripts");
                Application.OpenURL(url);
            });
            if (DataBuildersFoldout.IsActive)
            {
                m_DataBuildersRl.DoLayoutList();
                GUILayout.Space(postBlockContentSpace);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            GroupTemplateObjectsFoldout.IsActive = AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(GroupTemplateObjectsFoldout.IsActive, m_GroupTemplateObjectsHeader, () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("editor/AddressableAssetSettings.html#asset-group-templates");
                Application.OpenURL(url);
            });
            if (GroupTemplateObjectsFoldout.IsActive)
            {
                m_GroupTemplateObjectsRl.DoLayoutList();
                GUILayout.Space(postBlockContentSpace);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            if (EditorGUI.EndChangeCheck() || m_QueuedChanges.Count > 0)
            {
                Undo.RecordObject(m_AasTarget, "AddressableAssetSettings before changes");
                foreach (var change in m_QueuedChanges)
                {
                    change.Invoke();
                }

                m_AasTarget.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                serializedObject.ApplyModifiedProperties();
            }
        }

        void DrawDataBuilderCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            var so = m_AasTarget.DataBuilders[index];
            var builder = so as IDataBuilder;
            var label = builder == null ? "" : builder.Name;
            var nb = EditorGUI.ObjectField(rect, label, so, typeof(ScriptableObject), false) as ScriptableObject;
            if (nb != so)
                m_AasTarget.SetDataBuilderAtIndex(index, nb as IDataBuilder);
        }

        void OnRemoveDataBuilder(ReorderableList list)
        {
            m_AasTarget.RemoveDataBuilder(list.index);
        }

        void OnAddDataBuilder(Rect buttonRect, ReorderableList list)
        {
            var assetPath = EditorUtility.OpenFilePanelWithFilters("Data Builder", "Assets", new[] {"Data Builder", "asset"});
            if (string.IsNullOrEmpty(assetPath))
                return;
            var builder = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath.Substring(assetPath.IndexOf("Assets/", StringComparison.Ordinal)));
            if (!typeof(IDataBuilder).IsAssignableFrom(builder.GetType()))
            {
                Debug.LogWarningFormat("Asset at {0} does not implement the IDataBuilder interface.", assetPath);
                return;
            }

            m_AasTarget.AddDataBuilder(builder as IDataBuilder);
        }

        void DrawGroupTemplateObjectCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            var so = m_AasTarget.GroupTemplateObjects[index];
            var groupTObj = so as IGroupTemplate;
            ScriptableObject newObj = null;
            if (groupTObj == null)
            {
                newObj = EditorGUI.ObjectField(rect, "Missing", null, typeof(ScriptableObject), false) as ScriptableObject;
            }
            else
            {
                newObj = EditorGUI.ObjectField(rect, groupTObj.Name, so, typeof(ScriptableObject), false) as ScriptableObject;
            }

            if (newObj != so)
                m_AasTarget.SetGroupTemplateObjectAtIndex(index, newObj as IGroupTemplate);
        }

        void OnRemoveGroupTemplateObject(ReorderableList list)
        {
            m_AasTarget.RemoveGroupTemplateObject(list.index);
        }

        void OnAddGroupTemplateObject(Rect buttonRect, ReorderableList list)
        {
            var assetPath = EditorUtility.OpenFilePanelWithFilters("Assets Group Templates", "Assets", new[] {"Group Template Object", "asset"});
            if (string.IsNullOrEmpty(assetPath))
                return;
            if (assetPath.StartsWith(Application.dataPath, StringComparison.Ordinal) == false)
            {
                Debug.LogWarningFormat("Path at {0} is not an Asset of this project.", assetPath);
                return;
            }

            string relativePath = assetPath.Remove(0, Application.dataPath.Length - 6);
            var templateObj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(relativePath);
            if (templateObj == null)
            {
                Debug.LogWarningFormat("Failed to load Asset at {0}.", assetPath);
                return;
            }

            if (!typeof(IGroupTemplate).IsAssignableFrom(templateObj.GetType()))
            {
                Debug.LogWarningFormat("Asset at {0} does not implement the IGroupTemplate interface.", assetPath);
                return;
            }

            m_AasTarget.AddGroupTemplateObject(templateObj as IGroupTemplate);
        }

    }
}
