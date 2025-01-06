using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets.Util
{
    public static class AsyncOpPayloads
    {
        private static readonly Dictionary<int, object> _objData = new();
        private static readonly Dictionary<int, Scene> _sceneData = new();

        public static void SetData(AsyncOperation op, object payload)
        {
            _objData.Add(op.GetHashCode(), payload);
            Assert.IsTrue(_objData.Count < 128, "AsyncOpPayloads is growing too large: " + _objData.Count);
        }

        public static object PopData(AsyncOperation op)
        {
            return _objData.Remove(op.GetHashCode(), out var payload)
                ? payload : throw new KeyNotFoundException("Payload not found");
        }

        public static void SetScene(AsyncOperation op, Scene payload)
        {
            _sceneData.Add(op.GetHashCode(), payload);
            Assert.IsTrue(_sceneData.Count < 4, "AsyncOpPayloads is growing too large");
        }

        public static Scene PopScene(AsyncOperation op)
        {
            return _sceneData.Remove(op.GetHashCode(), out var payload)
                ? payload : throw new KeyNotFoundException("Payload not found");
        }
    }
}