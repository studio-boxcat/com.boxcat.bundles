using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets.ResourceProviders
{
    internal class BundledAssetProvider : IResourceProvider
    {
        public AsyncOperation LoadAsync<T>(AssetBundle bundle, Address address)
        {
            var name = address.Name();
#if DEBUG
            var startTime = Time.unscaledTime;
            L.I($"[Addressables] AssetBundle.LoadAssetAsync: key={name}, bundle={bundle.name}, address={address}, startTime={startTime}");
#endif
            Assert.IsTrue(bundle.Contains(name), $"Bundle does not contain asset: {name}");

            var op = bundle.LoadAssetAsync(name, typeof(T));
#if DEBUG
            op.completed += op2 =>
            {
                var deltaTime = Time.unscaledTime - startTime;
                L.I("[Addressables] AssetBundle.LoadAssetAsync completed: " +
                    $"key={name}, asset={((AssetBundleRequest) op2).asset}, bundle={bundle.name}, address={address}, time={deltaTime}");
            };
#endif
            return op;
        }

        public object GetResult(AsyncOperation op)
        {
            return ((AssetBundleRequest) op).asset;
        }
    }
}