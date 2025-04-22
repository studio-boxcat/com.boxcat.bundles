using JetBrains.Annotations;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets
{
    public static class Addressables
    {
        [CanBeNull]
        private static IAddressablesImpl _implCache;
        private static IAddressablesImpl _impl => _implCache ??= new AddressablesImpl(PathConfig.CatalogUri);

        [MustUseReturnValue]
        public static IAssetOp<TObject> LoadAssetAsync<TObject>(Address key) where TObject : Object
        {
            L.I($"[Addressables] Load Start: {key} ({typeof(TObject).Name}) ~ {_impl.GetType().Name}");
            var op = _impl.LoadAssetAsync<TObject>(key);
#if DEBUG
            op.AddOnComplete(static (o, payload) => L.I($"[Addressables] Load Done: {payload} - {o.name}"), key);
#endif
            return op;
        }

        [MustUseReturnValue]
        public static TObject LoadAsset<TObject>(Address key) where TObject : Object
        {
            L.I($"[Addressables] Load: {key} ({typeof(TObject).Name}) ~ {_impl.GetType().Name}");
            return _impl.LoadAsset<TObject>(key);
        }

        [MustUseReturnValue]
        public static IAssetOp<TObject> LoadAssetAsync<TObject>(AssetLocation key) where TObject : Object
        {
            L.I($"[Addressables] Load Start: {key} ({typeof(TObject).Name}) ~ {_impl.GetType().Name}");
            var op = _impl.LoadAssetAsync<TObject>(key);
#if DEBUG
            op.AddOnComplete(static (o, payload) => L.I($"[Addressables] Load Done: {payload} - {o.name}"), key);
#endif
            return op;
        }


        [MustUseReturnValue]
        public static TObject LoadAsset<TObject>(AssetLocation key) where TObject : Object
        {
            L.I($"[Addressables] Load: {key} ({typeof(TObject).Name}) ~ {_impl.GetType().Name}");
            return _impl.LoadAsset<TObject>(key);
        }

        public static IAssetOp<Scene> LoadSceneAsync(Address key)
        {
            L.I($"[Addressables] Load Start: {key} (Scene) ~ {_impl.GetType().Name}");
            var op = _impl.LoadSceneAsync(key);
#if DEBUG
            op.AddOnComplete(static (o, payload) => L.I($"[Addressables] Load Done: {payload} - {o.name}"), key);
#endif
            return op;
        }

#if UNITY_EDITOR
        internal static void ForceSetImpl(IAddressablesImpl impl)
        {
            (_implCache as AddressablesImpl)?.Dispose();
            _implCache = impl;
        }
#endif
    }
}