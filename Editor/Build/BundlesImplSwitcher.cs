using UnityEditor;
using UnityEditor.ShortcutManagement;

namespace Bundles.Editor
{
    internal static class BundlesImplSwitcher
    {
        private static EditorBundlesImpl _impl;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            _impl = new EditorBundlesImpl(AssetCatalog.Default);
            B.ForceSetImpl(_impl);
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
                L.I("[BundlesImplSwitcher] Entered Edit Mode, use AssetDatabase");
                useCatalog = false; // use AssetDatabase on editor mode
            }
            else // ExitingEditMode
            {
                useCatalog = SessionState.GetInt(_useCatalogKey, 0) is 1; // default: use AssetDatabase
                L.I("[BundlesImplSwitcher] Exiting Edit Mode, use " + (useCatalog ? "Catalog" : "AssetDatabase"));
            }

            B.ForceSetImpl(useCatalog ? null : _impl);
        }

        private const string _useCatalogKey = "lTMyCwCl";
        [Shortcut("Bundles/Use AssetDatabase for Play Mode")]
        public static void UseAssetDatabaseForPlayMode() => SessionState.SetInt(_useCatalogKey, 0);
        [Shortcut("Bundles/Use Catalog for Play Mode")]
        public static void UseCatalogForEditorMode() => SessionState.SetInt(_useCatalogKey, 1);
    }
}