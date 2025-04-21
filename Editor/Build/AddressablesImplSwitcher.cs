using UnityEditor.ShortcutManagement;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets.Build
{
    internal static class AddressablesImplSwitcher
    {
        private static EditorAddressablesImpl _impl;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            _impl = new EditorAddressablesImpl(AddressableCatalog.Default);
            Addressables.ForceSetImpl(_impl);
            EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
        }

        private static void OnEditorPlayModeChanged(PlayModeStateChange state)
        {
            if (state is not PlayModeStateChange.EnteredEditMode
                and not PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            bool useCatalog;
            if (state is PlayModeStateChange.EnteredEditMode)
            {
                L.I("[Addressables] Entered Edit Mode, use AssetDatabase");
                useCatalog = false; // use AssetDatabase on editor mode
            }
            else
            {
                useCatalog = SessionState.GetInt(_useCatalogKey, 0) is 1; // default: use AssetDatabase
                L.I("[Addressables] Exiting Edit Mode, use " + (useCatalog ? "Catalog" : "AssetDatabase"));
            }

            Addressables.ForceSetImpl(useCatalog ? null : _impl);
        }

        private const string _useCatalogKey = "it08jspS";
        [Shortcut("Addressables/Use AssetDatabase for Play Mode")]
        public static void UseAssetDatabaseForPlayMode() => SessionState.SetInt(_useCatalogKey, 0);
        [Shortcut("Addressables/Use Catalog for Play Mode")]
        public static void UseCatalogForEditorMode() => SessionState.SetInt(_useCatalogKey, 1);
    }
}