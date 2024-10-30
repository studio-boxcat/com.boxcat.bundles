using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets.ResourceProviders
{
    class SceneProvider : IResourceProvider
    {
        public AsyncOperation LoadAsync<T>(AssetBundle bundle, Address address)
        {
            var name = address.Name();
            L.I($"[Addressables] SceneManager.LoadSceneAsync: {name}");
            Assert.AreEqual(typeof(Scene), typeof(T));
            Assert.IsTrue(bundle.Contains(name), $"Bundle does not contain scene: {name}");

            var op = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
            op.allowSceneActivation = true;
            var scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            AsyncOpPayloads.SetScene(op, scene);
#if DEBUG
            op.completed += _ => L.I($"[Addressables] SceneManager.LoadSceneAsync completed: {name}");
#endif
            return op;
        }

        public object GetResult(AsyncOperation op)
        {
            return AsyncOpPayloads.PopScene(op);
        }
    }
}