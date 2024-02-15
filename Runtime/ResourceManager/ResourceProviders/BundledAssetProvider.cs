using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets.ResourceProviders
{
    public class BundledAssetProvider : IResourceProvider
    {
        public AsyncOperation Execute(AssetBundle bundle, Address address)
        {
            var name = address.Name();
            Assert.IsTrue(bundle.Contains(name), $"Bundle does not contain asset: {name}");
            return bundle.LoadAssetAsync(name);
        }

        public object GetResult(AsyncOperation op)
        {
            return ((AssetBundleRequest) op).asset;
        }
    }
}