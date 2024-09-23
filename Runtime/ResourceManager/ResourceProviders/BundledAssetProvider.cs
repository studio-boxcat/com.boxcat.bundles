using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets.ResourceProviders
{
    class BundledAssetProvider : IResourceProvider
    {
        public AsyncOperation LoadAsync<T>(AssetBundle bundle, Address address)
        {
            var name = address.Name();
            Assert.IsTrue(bundle.Contains(name), $"Bundle does not contain asset: {name}");
            return bundle.LoadAssetAsync(name, typeof(T));
        }

        public object GetResult(AsyncOperation op)
        {
            return ((AssetBundleRequest) op).asset;
        }
    }
}