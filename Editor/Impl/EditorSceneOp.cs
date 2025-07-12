using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace Bundles.Editor
{
    internal class EditorSceneOp : IAssetOp<Scene>
    {
        private AsyncOperation _op;
        private Scene _scene;
        private Action<Scene> _onComplete;

        public EditorSceneOp(AssetGUID guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid.ToString());
            _op = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
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

        private void OnComplete(AsyncOperation asyncOperation)
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

        public void AddOnComplete(Action<IAssetOp<Scene>, Scene, object, int> onComplete, object payloadObj, int payloadInt)
        {
            if (_op is null)
            {
                Assert.IsTrue(_scene.IsValid() && _scene.isLoaded, "Scene is not loaded");
                onComplete.SafeInvoke(this, _scene, payloadObj, payloadInt);
                return;
            }

            _onComplete += scene => onComplete.SafeInvoke(this, scene, payloadObj, payloadInt);
        }
    }
}