using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace UnityEditor.AddressableAssets.Build
{
    /// <summary>
    /// Utility class for the Addressables Build Content process.
    /// </summary>
    public static class BuildUtility
    {
        /// <summary>
        /// Used during the build to check for unsaved scenes and provide a user popup if there are any.
        /// </summary>
        /// <returns>True if there were no unsaved scenes, or if user hits "Save and Continue" on popup.
        /// False if any scenes were unsaved, and user hits "Cancel" on popup.</returns>
        public static bool CheckModifiedScenesAndAskToSave()
        {
            var dirtyScenes = new List<Scene>();
            for (var i = 0; i < SceneManager.sceneCount; ++i)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty) dirtyScenes.Add(scene);
            }
            if (dirtyScenes.Count == 0)
                return true;

            if (EditorUtility.DisplayDialog(
                    "Unsaved Scenes",
                    "Modified Scenes must be saved to continue.",
                    "Save and Continue", "Cancel") is false)
            {
                return false;
            }

            EditorSceneManager.SaveScenes(dirtyScenes.ToArray());
            return true;
        }
    }
}