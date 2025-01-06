using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets.ResourceProviders
{
    internal class SceneProvider : IResourceProvider
    {
        public AsyncOperation LoadAsync<T>(AssetBundle bundle, Address address)
        {
            var name = address.Name();
#if DEBUG
            var startTime = Time.unscaledTime;
            L.I($"[Addressables] SceneManager.LoadSceneAsync: key={name}, bundle={bundle.name}, startTime={startTime}");
#endif
            Assert.AreEqual(typeof(Scene), typeof(T));
            Assert.IsTrue(bundle.Contains(name), $"Bundle does not contain scene: {name}");

            var op = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
            op.allowSceneActivation = true;
            var scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            AsyncOpPayloads.SetScene(op, scene);
#if DEBUG
            op.completed += _ =>
            {
                var deltaTime = Time.unscaledTime - startTime;
                L.I("[Addressables] SceneManager.LoadAssetAsync completed: " +
                    $"key={name}, bundle={bundle.name}, time={deltaTime}");
            };
#endif
            return op;
        }

        public object GetResult(AsyncOperation op)
        {
            return AsyncOpPayloads.PopScene(op);
        }
    }
}