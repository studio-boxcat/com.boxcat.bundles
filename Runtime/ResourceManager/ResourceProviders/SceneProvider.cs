using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets.ResourceProviders
{
    public class SceneProvider : IResourceProvider
    {
        public AsyncOperation LoadAsync<T>(AssetBundle bundle, Address address)
        {
            Assert.AreEqual(typeof(Scene), typeof(T));
            Assert.IsTrue(bundle.Contains(address.Name()), $"Bundle does not contain scene: {address.Name()}");
            var op = SceneManager.LoadSceneAsync(address.Name(), LoadSceneMode.Additive);
            op.allowSceneActivation = true;
            var scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            AsyncOpPayloads.SetScene(op, scene);
            return op;
        }

        public object GetResult(AsyncOperation op)
        {
            return AsyncOpPayloads.PopScene(op);
        }
    }
}