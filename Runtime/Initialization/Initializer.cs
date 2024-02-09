using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace UnityEngine.AddressableAssets.Initialization
{
    static class Initializer
    {
        internal static void Initialize(AddressablesImpl addressables, out string error)
        {
            // On Android and WebGL platforms, it’s not possible to access the streaming asset files directly via file system APIs and streamingAssets path because these platforms return a URL. Use the UnityWebRequest class to access the content instead.
            var url = "file://" + ResourcePath.RuntimePath_CatalogBin;
#if DEBUG
            Debug.Log("[Addressables] InitializationOperation: " + url);
#endif
            var req = UnityWebRequest.Get(url).SendWebRequest();

            // Wait for completion.
            // TODO: Change to async.
            while (req.isDone is false)
            {
            }

            if (req.webRequest.result is not UnityWebRequest.Result.Success)
            {
                error = req.webRequest.error;
                return;
            }

            var reader = new BinaryStorageBuffer.Reader(req.webRequest.downloadHandler.data,
                1024, new ContentCatalogData.Serializer());
            var locMap = new ContentCatalogData.ResourceLocator(reader, 100);
            addressables.SetResourceLocator(locMap);

            // XXX: Hardcoded all used providers.
            var rp = Addressables.ResourceManager.ResourceProviders;
            rp[(int) ResourceProviderType.AssetBundle] = new AssetBundleProvider();
            rp[(int) ResourceProviderType.BundledAsset] = new BundledAssetProvider();

            error = null;
        }
    }
}