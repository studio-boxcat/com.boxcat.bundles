using JetBrains.Annotations;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using UnityEngine.AddressableAssets.AsyncOperations;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Entry point for Addressable API, this provides a simpler interface than using ResourceManager directly as it assumes string address type.
    /// </summary>
    public static class Addressables
    {
        [CanBeNull] static IAddressablesImpl _implCache;
        static IAddressablesImpl _impl
        {
            get
            {
                if (_implCache is not null) return _implCache;
#if UNITY_EDITOR
                _implCache = EditorAddressablesImplFactory.Create();
                if (_implCache is not null) return _implCache;
#endif
                return _implCache = new AddressablesImpl(LoadCatalog());
            }
        }

        public static IAssetOp<TObject> LoadAssetAsync<TObject>(string key) where TObject : Object
        {
            L.I($"[Addressables] Load Start: {key} ({typeof(TObject).Name}) ~ {_impl.GetType().Name}");
            var op = _impl.LoadAssetAsync<TObject>(key);
#if DEBUG
            op.AddOnComplete(o => L.I("[Addressables] Load Done: " + key + " - " + o.name));
#endif
            return op;
        }

        public static IAssetOp<Scene> LoadSceneAsync(string key)
        {
            L.I($"[Addressables] Load Start: {key} ~ {_impl.GetType().Name})");
            var op = _impl.LoadSceneAsync(key);
#if DEBUG
            op.AddOnComplete(o => L.I("[Addressables] Load Done: " + key + " - " + o.name));
#endif
            return op;
        }

        static ResourceCatalog LoadCatalog()
        {
#if DEBUG
            AssetBundle.UnloadAllAssetBundles(true);
#endif

            // On Android and WebGL platforms, it’s not possible to access the streaming asset files directly
            // via file system APIs and streamingAssets path because these platforms return a URL.
            // Use the UnityWebRequest class to access the content instead.
            var url = "file://" + PathConfig.RuntimePath_CatalogBin;
            L.I("[Addressables] PreloadCatalog: " + url);
            var req = UnityWebRequest.Get(url).SendWebRequest();
            req.WaitForComplete();
            Assert.AreEqual(req.webRequest.result, UnityWebRequest.Result.Success, "Failed to load catalog.");
            return new ResourceCatalog(req.webRequest.downloadHandler.data);
        }

#if UNITY_EDITOR
        static Addressables()
        {
            UnityEditor.EditorApplication.playModeStateChanged += change =>
            {
                if (change is not UnityEditor.PlayModeStateChange.EnteredEditMode) return;
                L.I("[Addressables] Purge");
                _implCache = null;
            };
        }

        internal static void Purge()
        {
            if (_implCache is null)
                return;

            L.I("[Addressables] Purge");
            _implCache = null;
        }
#endif
    }
}