using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets.ResourceProviders
{
    public class BundledAssetProvider : IResourceProvider
    {
        public AsyncOperation LoadAsync(AssetBundle bundle, Address address)
        {
            var name = address.Name();
            Assert.IsTrue(bundle.Contains(name), $"Bundle does not contain asset: {name}");
            var req = bundle.LoadAssetAsync(name);
            req.allowSceneActivation = true;
            return req;
        }

        public object GetResult(AsyncOperation op)
        {
            return ((AssetBundleRequest) op).asset;
        }
    }
}