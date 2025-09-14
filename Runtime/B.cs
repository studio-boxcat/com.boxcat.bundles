using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bundles
{
    public static class B
    {
        [CanBeNull]
        private static IBundlesImpl _implCache;
        private static IBundlesImpl _impl => _implCache ??= new BundlesImpl(Paths.CatalogUri);

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

        // helpers
        public static IAssetOp<Object> Load(Address key) => Load<Object>(key);
        public static IAssetOp<Object> Load(AssetLocation key) => Load<Object>(key);
        public static Object LoadSync(Address key) => LoadSync<Object>(key);
        public static Object LoadSync(AssetLocation key) => LoadSync<Object>(key);

        public static IAssetOp<GameObject> Prefab(Address key) => Load<GameObject>(key);
        public static IAssetOp<GameObject> Prefab(AssetLocation key) => Load<GameObject>(key);
        public static GameObject PrefabSync(Address key) => LoadSync<GameObject>(key);
        public static GameObject PrefabSync(AssetLocation key) => LoadSync<GameObject>(key);
        public static IAssetOp<ScriptableObject> SO(Address key) => Load<ScriptableObject>(key);
        public static IAssetOp<ScriptableObject> SO(AssetLocation key) => Load<ScriptableObject>(key);
        public static ScriptableObject SOSync(Address key) => LoadSync<ScriptableObject>(key);
        public static ScriptableObject SOSync(AssetLocation key) => LoadSync<ScriptableObject>(key);
        public static IAssetOp<Texture2D> Tex(Address key) => Load<Texture2D>(key);
        public static IAssetOp<Texture2D> Tex(AssetLocation key) => Load<Texture2D>(key);
        public static Texture2D TexSync(Address key) => LoadSync<Texture2D>(key);
        public static Texture2D TexSync(AssetLocation key) => LoadSync<Texture2D>(key);
        public static IAssetOp<Sprite> Sprite(Address key) => Load<Sprite>(key);
        public static IAssetOp<Sprite> Sprite(AssetLocation key) => Load<Sprite>(key);
        public static Sprite SpriteSync(Address key) => LoadSync<Sprite>(key);
        public static Sprite SpriteSync(AssetLocation key) => LoadSync<Sprite>(key);
        public static IAssetOp<Mesh> Mesh(Address key) => Load<Mesh>(key);
        public static IAssetOp<Mesh> Mesh(AssetLocation key) => Load<Mesh>(key);
        public static Mesh MeshSync(Address key) => LoadSync<Mesh>(key);
        public static Mesh MeshSync(AssetLocation key) => LoadSync<Mesh>(key);
        public static IAssetOp<Material> Mat(Address key) => Load<Material>(key);
        public static IAssetOp<Material> Mat(AssetLocation key) => Load<Material>(key);
        public static Material MatSync(Address key) => LoadSync<Material>(key);
        public static Material MatSync(AssetLocation key) => LoadSync<Material>(key);
        public static IAssetOp<Font> Font(Address key) => Load<Font>(key);
        public static IAssetOp<Font> Font(AssetLocation key) => Load<Font>(key);
        public static Font FontSync(Address key) => LoadSync<Font>(key);
        public static Font FontSync(AssetLocation key) => LoadSync<Font>(key);


#if UNITY_EDITOR
        internal static void ForceSetImpl(IBundlesImpl impl)
        {
            (_implCache as BundlesImpl)?.Dispose();
            _implCache = impl;
        }
#endif
    }
}