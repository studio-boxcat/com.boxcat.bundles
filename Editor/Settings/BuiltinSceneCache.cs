using System;
using System.Collections.Generic;

namespace UnityEditor.AddressableAssets
{
    internal static class BuiltinSceneCache
    {
        internal static EditorBuildSettingsScene[] m_Scenes;
        private static Dictionary<AssetGUID, int> s_GUIDSceneIndexLookup;
        private static bool s_IsListening;
        public static event Action sceneListChanged;

        public static EditorBuildSettingsScene[] scenes
        {
            get
            {
                if (m_Scenes == null)
                {
                    if (!s_IsListening)
                    {
                        s_IsListening = true;
                        EditorBuildSettings.sceneListChanged += EditorBuildSettings_sceneListChanged;
                    }

                    InvalidateCache();
                    m_Scenes = EditorBuildSettings.scenes;
                }

                return m_Scenes;
            }
            set => EditorBuildSettings.scenes = value;
        }

        public static Dictionary<AssetGUID, int> GUIDSceneIndexLookup
        {
            get
            {
                if (s_GUIDSceneIndexLookup != null)
                    return s_GUIDSceneIndexLookup;

                var localScenes = scenes;
                s_GUIDSceneIndexLookup = new Dictionary<AssetGUID, int>();
                var enabledIndex = 0;
                for (var i = 0; i < scenes.Length; i++)
                {
                    if (localScenes[i] != null && localScenes[i].enabled)
                        s_GUIDSceneIndexLookup[(AssetGUID) localScenes[i].guid] = enabledIndex++;
                }

                return s_GUIDSceneIndexLookup;
            }
        }

        private static void InvalidateCache()
        {
            m_Scenes = null;
            s_GUIDSceneIndexLookup = null;
        }

        private static void EditorBuildSettings_sceneListChanged()
        {
            InvalidateCache();
            if (sceneListChanged != null)
                sceneListChanged();
        }
    }
}
