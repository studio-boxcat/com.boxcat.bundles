#if UNITY_EDITOR
using System;
using UnityEngine.Assertions;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets.AsyncOperations
{
    public class EditorSceneOp : IAssetOp<Scene>
    {
        AsyncOperation _op;
        Scene _scene;
        Action<Scene> _onComplete;

        public EditorSceneOp(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                path = "Assets/" + path;
            if (path.LastIndexOf(".unity", StringComparison.OrdinalIgnoreCase) == -1)
                path += ".unity";

            _op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            _op.completed += OnComplete;
            var scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            AsyncOpPayloads.SetScene(_op, scene);
        }

        public override string ToString() => "EditorSceneOp:" + (_op?.ToString() ?? _scene.name);

        public bool TryGetResult(out Scene result)
        {
            if (_op is not null)
            {
                if (_op.isDone)
                {
                    OnComplete(_op);
                    Assert.IsTrue(_scene.IsValid(), "Scene is not valid");
                }
            }

            if (_scene.IsValid() is false)
            {
                result = default;
                return false;
            }

            Assert.IsTrue(_scene.isLoaded, "Scene is not loaded");
            result = _scene;
            return true;
        }

        public Scene WaitForCompletion()
        {
            if (_op is not null)
                throw new NotSupportedException("Sync operation is not supported");

            Assert.IsTrue(_scene.IsValid(), "Scene is not valid");
            Assert.IsTrue(_scene.isLoaded, "Scene is not loaded");
            return _scene;
        }

        void OnComplete(AsyncOperation asyncOperation)
        {
            // OnComplete will be called twice if the Result property is called before the task is complete.
            if (_scene.IsValid())
            {
                Assert.IsTrue(_scene.isLoaded, "Scene is not loaded");
                Assert.IsNull(_op, "Operation should be null after completion");
                return;
            }

            Assert.AreEqual(_op, asyncOperation, "Operation mismatch");
            Assert.IsNotNull(_op, "Operation is null");
            _scene = AsyncOpPayloads.PopScene(_op);
            Assert.IsTrue(_scene.IsValid(), "Scene is not valid");
            Assert.IsTrue(_scene.isLoaded, "Scene is not loaded");
            _op = null;

            var onComplete = _onComplete;
            _onComplete = null;
            onComplete?.Invoke(_scene);
        }

        public void AddOnComplete(Action<Scene> onComplete)
        {
            if (_op is null)
            {
                Assert.IsTrue(_scene.IsValid() && _scene.isLoaded, "Scene is not loaded");
                onComplete.SafeInvoke(_scene);
                return;
            }

            _onComplete += onComplete;
        }

        public void AddOnComplete(Action<Scene, object> onComplete, object payload)
            => AddOnComplete(obj => onComplete(obj, payload));

        public void AddOnComplete(Action<IAssetOp<Scene>, Scene, object> onComplete, object payload)
            => AddOnComplete(obj => onComplete(this, obj, payload));
    }
}
#endif