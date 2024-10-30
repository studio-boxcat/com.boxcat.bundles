using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets.ResourceProviders
{
    class BundledAssetProvider : IResourceProvider
    {
        public AsyncOperation LoadAsync<T>(AssetBundle bundle, Address address)
        {
            var name = address.Name();
            L.I($"[Addressables] AssetBundle.LoadAssetAsync: {name}");
            Assert.IsTrue(bundle.Contains(name), $"Bundle does not contain asset: {name}");

            var op = bundle.LoadAssetAsync(name, typeof(T));
#if DEBUG
            op.completed += op2 => L.I($"[Addressables] AssetBundle.LoadAssetAsync completed: key={name}, asset={((AssetBundleRequest) op2).asset}");
#endif
            return op;
        }

        public object GetResult(AsyncOperation op)
        {
            return ((AssetBundleRequest) op).asset;
        }
    }
}