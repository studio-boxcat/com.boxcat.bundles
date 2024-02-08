using System.IO;
using System.Linq;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Networking;

namespace UnityEngine.AddressableAssets.Initialization
{
    internal class InitializationOperation : AsyncOperationBase<IResourceLocator>
    {
        AddressablesImpl m_Addressables;

        public InitializationOperation(AddressablesImpl aa)
        {
            m_Addressables = aa;
        }

        protected override string DebugName => "InitializationOperation";

        internal static AsyncOperationHandle<IResourceLocator> CreateInitializationOperation(AddressablesImpl aa)
        {
            var initOp = new InitializationOperation(aa);
            return aa.ResourceManager.StartOperation(initOp, default);
        }

        /// <inheritdoc />
        protected override bool InvokeWaitForCompletion()
        {
            if (IsDone)
                return true;

            if (!HasExecuted)
                InvokeExecute();

            return true;
        }

        protected override void Execute()
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
                Complete(null, false, req.webRequest.error);
                return;
            }

            var reader = new BinaryStorageBuffer.Reader
                (req.webRequest.downloadHandler.data, 1024, new ContentCatalogData.Serializer());

            // XXX: Hardcoded all used providers.
            {
                var rp = Addressables.ResourceManager.ResourceProviders;
                if (rp.Any(p => p is AssetBundleProvider) is false)
                {
                    var abp = new AssetBundleProvider();
                    abp.Initialize(typeof(AssetBundleProvider).FullName);
                    rp.Add(abp);
                }

                if (rp.Any(p => p is BundledAssetProvider) is false)
                {
                    var bap = new BundledAssetProvider();
                    bap.Initialize(typeof(BundledAssetProvider).FullName);
                    rp.Add(bap);
                }
            }

            m_Addressables.InstanceProvider ??= new InstanceProvider();
            m_Addressables.SceneProvider ??= new SceneProvider();

            var locMap = new ContentCatalogData.ResourceLocator(reader, 100);
            m_Addressables.AddResourceLocator(locMap);
            m_Addressables.AddResourceLocator(new DynamicResourceLocator(m_Addressables));
            m_Addressables.ResourceManager.CreateCompletedOperation(locMap, string.Empty);

            Complete(locMap, true, "");
        }
    }
}