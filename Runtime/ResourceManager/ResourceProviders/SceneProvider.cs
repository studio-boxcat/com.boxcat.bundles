using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets.ResourceProviders
{
    public class SceneProvider : IResourceProvider
    {
        public AsyncOperation Execute(AssetBundle bundle, Address address)
        {
            Assert.IsTrue(bundle.Contains(address.Name()), $"Bundle does not contain scene: {address.Name()}");
            var op = SceneManager.LoadSceneAsync(address.Name(), LoadSceneMode.Additive);
            op.allowSceneActivation = true;
            var scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            AsyncOpPayloads.SetScene(op, scene);
            return op;
        }

        public object GetResult(AsyncOperation op)
        {
            // TODO: Reduce heap allocations
            return AsyncOpPayloads.PopScene(op);
        }
    }
}