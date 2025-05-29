using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bundles
{
    public static class B
    {
        [CanBeNull]
        private static IBundlesImpl _implCache;
        private static IBundlesImpl _impl => _implCache ??= new BundlesImpl(PathConfig.CatalogUri);

        [MustUseReturnValue]
        public static IAssetOp<TObject> Load<TObject>(Address key) where TObject : Object
        {
            L.I($"[B] Load Start: {key.Name()} ({typeof(TObject).Name}) ~ {_impl.GetType().Name}");
            var op = _impl.Load<TObject>(key);
#if DEBUG
            op.AddOnComplete(static (o, payload) => L.I($"[B] Load Done: {payload} - {o.name}"), key.Name());
#endif
            return op;
        }

        [MustUseReturnValue]
        public static TObject LoadSync<TObject>(Address key) where TObject : Object
        {
            L.I($"[B] Load: {key.Name()} ({typeof(TObject).Name}) ~ {_impl.GetType().Name}");
            return _impl.LoadSync<TObject>(key);
        }

        [MustUseReturnValue]
        public static IAssetOp<TObject> Load<TObject>(AssetLocation key) where TObject : Object
        {
            L.I($"[B] Load Start: {key} ({typeof(TObject).Name}) ~ {_impl.GetType().Name}");
            var op = _impl.Load<TObject>(key);
#if DEBUG
            op.AddOnComplete(static (o, payload) => L.I($"[B] Load Done: {payload} - {o.name}"), key);
#endif
            return op;
        }


        [MustUseReturnValue]
        public static TObject LoadSync<TObject>(AssetLocation key) where TObject : Object
        {
            L.I($"[B] Load: {key} ({typeof(TObject).Name}) ~ {_impl.GetType().Name}");
            return _impl.LoadSync<TObject>(key);
        }

        public static IAssetOp<Scene> LoadScene(Address key)
        {
            L.I($"[B] Load Start: {key.Name()} (Scene) ~ {_impl.GetType().Name}");
            var op = _impl.LoadScene(key);
#if DEBUG
            op.AddOnComplete(static (o, payload) => L.I($"[B] Load Done: {payload} - {o.name}"), key.Name());
#endif
            return op;
        }

#if UNITY_EDITOR
        internal static void ForceSetImpl(IBundlesImpl impl)
        {
            (_implCache as BundlesImpl)?.Dispose();
            _implCache = impl;
        }
#endif
    }
}