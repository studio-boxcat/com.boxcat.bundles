#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Assertions;

namespace Bundles.Editor
{
    internal interface IEditorAssetOp
    {
        void LoadImmediate();
    }

    internal class EditorAssetOp<TObject> : IAssetOp<TObject>, IEditorAssetOp where TObject : UnityEngine.Object
    {
        private readonly string _path;
        private readonly float _loadTime;

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

            var loadTime = EditorAssetLoadLoop.GetTime() + loadDelay;
            EditorAssetLoadLoop.Queue(this, loadTime);
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
            if (EditorAssetLoadLoop.GetTime() >= _loadTime)
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

        public void LoadImmediate()
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

    internal static class EditorAssetLoadLoop
    {
        private static readonly List<(IEditorAssetOp, float)> _pending = new();

        public static float GetTime() => (float) EditorApplication.timeSinceStartup;

        public static void Queue(IEditorAssetOp op, float loadTime)
        {
            if (_pending.IsEmpty())
                EditorApplication.update += (_update ??= Update);
            _pending.Add((op, loadTime));
        }

        private static EditorApplication.CallbackFunction? _update;

        private static void Update()
        {
            var t = GetTime();

            // reverse loop to mitigate re-entry issues
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                var (op, loadTime) = _pending[i];
                if (t >= loadTime)
                {
                    op.LoadImmediate();
                    _pending.RemoveAt(i);
                }
            }

            if (_pending.IsEmpty())
                EditorApplication.update -= _update;
        }
    }
}