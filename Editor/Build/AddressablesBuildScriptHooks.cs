using UnityEngine.AddressableAssets.Util;

namespace UnityEditor.AddressableAssets.Build
{
    static class AddressablesBuildScriptHooks
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
            EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
        }

        static void OnEditorPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;

            var builder = settings.ActivePlayModeDataBuilder;
            if (builder == null)
            {
                L.E("Active play mode build script is null.");
                return;
            }

            if (!builder.CanBuildData<AddressablesPlayModeBuildResult>())
            {
                L.E($"Active build script {builder} cannot build AddressablesPlayModeBuildResult.");
                return;
            }

            L.I("[Addressables] BuildScriptHooks: " + settings.ActivePlayModeDataBuilder.Name);
            var res = settings.ActivePlayModeDataBuilder.BuildData<AddressablesPlayModeBuildResult>(new AddressablesDataBuilderInput(settings));
            if (!string.IsNullOrEmpty(res.Error))
            {
                L.E(res.Error);
                EditorApplication.isPlaying = false;
            }
        }
    }
}