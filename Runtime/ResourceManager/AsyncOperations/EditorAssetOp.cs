#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets.AsyncOperations
{
    public class EditorAssetOp<TObject> : IAssetOp<TObject> where TObject : Object
    {
        readonly string _path;
        readonly DateTime _loadTime;

        TObject _result;
        Action<TObject> _onComplete;

        public EditorAssetOp(string path)
        {
            _path = path;

            var loadDelay = GetDelay();
            if (loadDelay == 0)
            {
                LoadImmediate();
                Assert.IsNotNull(_result, $"Failed to load asset at path: {_path}");
                return;
            }

            _loadTime = DateTime.Now.AddSeconds(loadDelay);
            Task.Delay((int) (loadDelay * 1000))
                .ContinueWith(_ => LoadImmediate());
        }

        public bool IsDone => _result is not null || _loadTime >= DateTime.Now;

        public TObject Result
        {
            get
            {
                if (_result is not null)
                    return _result;
                LoadImmediate();
                Assert.IsNotNull(_result, $"Failed to load asset at path: {_path}");
                return _result;
            }
        }

        public void AddOnComplete(Action<TObject> onComplete)
        {
            if (IsDone)
            {
                Assert.IsNotNull(Result, $"Failed to load asset at path: {_path}");
                onComplete.SafeInvoke(Result);
                return;
            }

            _onComplete += onComplete;
        }

        public void AddOnComplete(Action<TObject, object> onComplete, object payload)
            => AddOnComplete(obj => onComplete(obj, payload));

        void LoadImmediate()
        {
            // LoadImmediate will be called twice if the Result property is called before the task is complete.
            if (_result is not null)
                return;

            _result = AssetDatabase.LoadAssetAtPath<TObject>(_path);

            var onComplete = _onComplete;
            _onComplete = null;
            onComplete?.Invoke(_result);
        }

        static float GetDelay()
        {
            var noDelay = Random.value < 0.05f; // 5% chance of no delay.
            if (noDelay) return 0f;
            var loadDelay = Random.Range(0, 0.3f); // 0s - 0.3s delay.
            return loadDelay;
        }

        public string GetDebugName()
        {
            return $"EditorAssetOp:{_path} ({(IsDone ? "Loaded" : "Loading")})";
        }
    }
}

#endif