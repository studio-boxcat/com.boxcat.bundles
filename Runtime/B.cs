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

        // helpers
        public static IAssetOp<Object> Load(Address key) => Load<Object>(key);
        public static IAssetOp<Object> Load(AssetLocation key) => Load<Object>(key);
        public static Object LoadSync(Address key) => LoadSync<Object>(key);
        public static Object LoadSync(AssetLocation key) => LoadSync<Object>(key);
        public static IAssetOp<GameObject> LoadPrefab(Address key) => Load<GameObject>(key);
        public static IAssetOp<GameObject> LoadPrefab(AssetLocation key) => Load<GameObject>(key);
        public static GameObject LoadPrefabSync(Address key) => LoadSync<GameObject>(key);
        public static GameObject LoadPrefabSync(AssetLocation key) => LoadSync<GameObject>(key);
        public static IAssetOp<Texture2D> LoadTexture(Address key) => Load<Texture2D>(key);
        public static IAssetOp<Texture2D> LoadTexture(AssetLocation key) => Load<Texture2D>(key);
        public static Texture2D LoadTextureSync(Address key) => LoadSync<Texture2D>(key);
        public static Texture2D LoadTextureSync(AssetLocation key) => LoadSync<Texture2D>(key);
        public static IAssetOp<Sprite> LoadSprite(Address key) => Load<Sprite>(key);
        public static IAssetOp<Sprite> LoadSprite(AssetLocation key) => Load<Sprite>(key);
        public static Sprite LoadSpriteSync(Address key) => LoadSync<Sprite>(key);
        public static Sprite LoadSpriteSync(AssetLocation key) => LoadSync<Sprite>(key);
        public static IAssetOp<Mesh> LoadMesh(Address key) => Load<Mesh>(key);
        public static IAssetOp<Mesh> LoadMesh(AssetLocation key) => Load<Mesh>(key);
        public static Mesh LoadMeshSync(Address key) => LoadSync<Mesh>(key);
        public static Mesh LoadMeshSync(AssetLocation key) => LoadSync<Mesh>(key);
        public static IAssetOp<Material> LoadMaterial(Address key) => Load<Material>(key);
        public static IAssetOp<Material> LoadMaterial(AssetLocation key) => Load<Material>(key);
        public static Material LoadMaterialSync(Address key) => LoadSync<Material>(key);
        public static Material LoadMaterialSync(AssetLocation key) => LoadSync<Material>(key);


#if UNITY_EDITOR
        internal static void ForceSetImpl(IBundlesImpl impl)
        {
            (_implCache as BundlesImpl)?.Dispose();
            _implCache = impl;
        }
#endif
    }
}