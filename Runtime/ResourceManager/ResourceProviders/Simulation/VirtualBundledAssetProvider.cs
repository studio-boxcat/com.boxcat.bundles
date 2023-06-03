#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace UnityEngine.ResourceManagement.ResourceProviders.Simulation
{
    /// <summary>
    /// Custom version of AssetBundleRequestOptions used to compute needed download size while bypassing the cache.  In the future a virtual cache may be implemented.
    /// </summary>
    [Serializable]
    public class VirtualAssetBundleRequestOptions : AssetBundleRequestOptions
    {
        /// <inheritdoc/>
        public override long ComputeSize(IResourceLocation location, ResourceManager resourceManager)
        {
            return 0;
        }
    }

    /// <summary>
    /// Provides assets from virtual asset bundles.  Actual loads are done through the AssetDatabase API.
    /// </summary>
    [DisplayName("Assets from Virtual Bundles")]
    public class VirtualBundledAssetProvider : ResourceProviderBase
    {
        /// <summary>
        /// Default copnstructor.
        /// </summary>
        public VirtualBundledAssetProvider()
        {
            m_ProviderId = typeof(BundledAssetProvider).FullName;
        }

        class InternalOp
        {
            VBAsyncOperation<object> m_RequestOperation;
            ProvideHandle m_PI;

            public void Start(ProvideHandle provideHandle, VirtualAssetBundle bundle)
            {
                m_PI = provideHandle;
                provideHandle.SetWaitForCompletionCallback(WaitForCompletionHandler);
                m_RequestOperation = bundle.LoadAssetAsync(m_PI, m_PI.Location);
                m_RequestOperation.Completed += RequestOperation_Completed;
            }

            private bool WaitForCompletionHandler()
            {
                return m_RequestOperation.WaitForCompletion();
            }

            private void RequestOperation_Completed(VBAsyncOperation<object> obj)
            {
                bool success = (obj.Result != null && m_PI.Type.IsAssignableFrom(obj.Result.GetType())) && obj.OperationException == null;
                m_PI.Complete(obj.Result, success, obj.OperationException);
            }
        }

        public override void Provide(ProvideHandle provideHandle)
        {
            List<object> deps = new List<object>(); // TODO: garbage. need to pass actual count and reuse the list
            provideHandle.GetDependencies(deps);
            VirtualAssetBundle bundle = deps[0] as VirtualAssetBundle;
            if (bundle == null)
            {
                provideHandle.Complete<object>(null, false, new Exception($"Unable to load asset of type {provideHandle.Type} from location {provideHandle.Location}."));
            }
            else
            {
                new InternalOp().Start(provideHandle, bundle);
            }
        }
    }
}
#endif
