#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.AsyncOperations;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets
{
    internal class EditorAssetOp<TObject> : IAssetOp<TObject> where TObject : UnityEngine.Object
    {
        private readonly string _path;
        private readonly DateTime _loadTime;

        private TObject _result;
        private Action<TObject> _onComplete;

        public EditorAssetOp(string path)
        {
            _path = path;

            // load immediately if delay is 0
            var loadDelay = SimulateDelay();
            if (loadDelay == 0)
            {
                LoadImmediate();
                Assert.IsNotNull(_result, $"Failed to load asset: path={path}");
                return;
            }

            // otherwise, schedule the load
            _loadTime = DateTime.Now.AddSeconds(loadDelay);
            Task.Delay((int) (loadDelay * 1000)).ContinueWith(
                static (_, s) => EditorApplication.delayCall += ((EditorAssetOp<TObject>) s).LoadImmediate,
                this);
        }

        public override string ToString() => $"EditorAssetOp:{_path} ({(_result != default ? "Loaded" : "Loading")})";

        public bool TryGetResult(out TObject result)
        {
            if (_result is not null)
            {
                result = _result;
                return true;
            }

            // Even if _result is null, we still want to return true if the load time has passed.
            if (DateTime.Now >= _loadTime)
            {
                LoadImmediate();
                Assert.IsNotNull(_result, $"Failed to load asset at path: {_path}");
                result = _result;
                return true;
            }

            result = null;
            return false;
        }

        public TObject WaitForCompletion()
        {
            if (_result is not null)
                return _result;
            LoadImmediate();
            Assert.IsNotNull(_result, $"Failed to load asset at path: {_path}");
            return _result;
        }

        private void LoadImmediate()
        {
            // LoadImmediate will be called twice if the Result property is called before the task is complete.
            if (_result is not null)
                return;

            try
            {
                _result = AssetDatabase.LoadAssetAtPath<TObject>(_path);
            }
            catch (Exception e)
            {
                L.E("[EditorAssetOp] Failed to load asset: path=" + _path);
                L.E(e);
                throw;
            }

            L.I($"[EditorAssetOp] Loaded asset: path={_path}");
            var onComplete = _onComplete;
            _onComplete = null;
            onComplete?.Invoke(_result);
        }

        public void AddOnComplete(Action<TObject> onComplete)
        {
            if (TryGetResult(out var result))
            {
                Assert.IsNotNull(result, $"Failed to load asset: path={_path}");
                onComplete.SafeInvoke(result);
                return;
            }

            _onComplete += onComplete;
        }

        public void AddOnComplete(Action<TObject, object> onComplete, object payload)
            => AddOnComplete(obj => onComplete(obj, payload));

        public void AddOnComplete(Action<IAssetOp<TObject>, TObject, object> onComplete, object payload)
            => AddOnComplete(obj => onComplete(this, obj, payload));

        private static float SimulateDelay()
        {
            if (EditorConfig.NoAssetDatabaseDelaySimulation) return 0f;
            var noDelay = UnityEngine.Random.value < 0.05f; // 5% chance of no delay.
            if (noDelay) return 0f;
            var loadDelay = UnityEngine.Random.Range(0.05f, 0.15f); // 0.05s - 0.15s delay.
            return loadDelay;
        }
    }
}

#endif