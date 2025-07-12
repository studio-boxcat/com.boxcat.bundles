#nullable enable
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.Assertions;

namespace Bundles.Editor
{
    internal class EditorAssetOp<TObject> : IAssetOp<TObject> where TObject : UnityEngine.Object
    {
        private readonly string _path;
        private readonly DateTime _loadTime;

        private TObject? _result;
        private Action<TObject>? _onComplete;

        public EditorAssetOp(string path, float loadDelay)
        {
            _path = path;

            // load immediately if delay is 0
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

        public override string ToString() => $"EditorAssetOp:{_path} ({(_result is not null ? "Loaded" : "Loading")})";

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
                result = _result!;
                return true;
            }

            result = null!; // never use this value.
            return false;
        }

        public TObject WaitForCompletion()
        {
            if (_result is not null)
                return _result;
            LoadImmediate();
            Assert.IsNotNull(_result, $"Failed to load asset at path: {_path}");
            return _result!;
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

        public void AddOnComplete(Action<IAssetOp<TObject>, TObject, object, int> onComplete, object payloadObj, int payloadInt)
        {
            if (TryGetResult(out var result))
            {
                Assert.IsNotNull(result, $"Failed to load asset: path={_path}");
                onComplete.SafeInvoke(this, result, payloadObj, payloadInt);
                return;
            }

            _onComplete += obj => onComplete.SafeInvoke(this, obj, payloadObj, payloadInt);
        }
    }
}