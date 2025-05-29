using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace Bundles
{
    internal class SceneProvider : IResourceProvider
    {
        public AsyncOperation LoadAsync<T>(AssetBundle bundle, string assetName)
        {
#if DEBUG
            var startTime = Time.unscaledTime;
            L.I($"[B] SceneManager.LoadScene: name={assetName}, bundle={bundle.name}, startTime={startTime}");
#endif
            Assert.AreEqual(typeof(Scene), typeof(T));
            Assert.IsTrue(bundle.Contains(assetName), $"Bundle does not contain scene: {assetName}");

            var op = SceneManager.LoadSceneAsync(assetName, LoadSceneMode.Additive);
            op.allowSceneActivation = true;
            var scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            AsyncOpPayloads.SetScene(op, scene);
#if DEBUG
            op.completed += _ =>
            {
                var deltaTime = Time.unscaledTime - startTime;
                L.I("[B] SceneManager.Load completed: " +
                    $"name={assetName}, bundle={bundle.name}, time={deltaTime}");
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