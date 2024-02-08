#if UNITY_2022_1_OR_NEWER
#define UNLOAD_BUNDLE_ASYNC
#endif

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine.ResourceManagement.Exceptions;
using UnityEngine.ResourceManagement.ResourceLocations;


namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Wrapper for asset bundles.
    /// </summary>
    public interface IAssetBundleResource
    {
        /// <summary>
        /// Retrieves the asset bundle.
        /// </summary>
        /// <returns>Returns the asset bundle.</returns>
        AssetBundle GetAssetBundle();
    }

    /// <summary>
    /// Provides methods for loading an AssetBundle from a local or remote location.
    /// </summary>
    public class AssetBundleResource : IAssetBundleResource
    {
        AssetBundle m_AssetBundle;
        AsyncOperation m_RequestOperation;
        internal ProvideHandle m_ProvideHandle;

        [NonSerialized]
        bool m_RequestCompletedCallbackCalled = false;

        int m_Retries;
        bool m_Completed = false;
#if UNLOAD_BUNDLE_ASYNC
        AssetBundleUnloadOperation m_UnloadOperation;
#endif
        string m_TransformedInternalId;
        AssetBundleRequest m_PreloadRequest;
        bool m_PreloadCompleted = false;

        /// <summary>
        /// Creates a request for loading all assets from an AssetBundle.
        /// </summary>
        /// <returns>Returns the request.</returns>
        public AssetBundleRequest GetAssetPreloadRequest()
        {
            if (m_PreloadCompleted || GetAssetBundle() == null)
                return null;

            return null;
        }

        /// <summary>
        /// Get the asset bundle object managed by this resource.  This call may force the bundle to load if not already loaded.
        /// </summary>
        /// <returns>The asset bundle.</returns>
        public AssetBundle GetAssetBundle()
        {
            return m_AssetBundle;
        }

#if ENABLE_ADDRESSABLE_PROFILER
        private void AddBundleToProfiler(Profiling.ContentStatus status, BundleSource source)
        {
            if (!Profiler.enabled)
                return;
            if (!m_ProvideHandle.IsValid)
                return;

            if (status == Profiling.ContentStatus.Active && m_AssetBundle == null)
                Profiling.ProfilerRuntime.BundleReleased(m_Options.BundleName);
            else
                Profiling.ProfilerRuntime.AddBundleOperation(m_ProvideHandle, m_Options, status, source);
        }

        private void RemoveBundleFromProfiler()
        {
            if (m_Options == null)
                return;
            Profiling.ProfilerRuntime.BundleReleased(m_Options.BundleName);
        }
#endif

#if UNLOAD_BUNDLE_ASYNC
        void OnUnloadOperationComplete(AsyncOperation op)
        {
            m_UnloadOperation = null;
            BeginOperation();
        }

#endif

#if UNLOAD_BUNDLE_ASYNC
        /// <summary>
        /// Stores AssetBundle loading information, starts loading the bundle.
        /// </summary>
        /// <param name="provideHandle">The container for AssetBundle loading information.</param>
        /// <param name="unloadOp">The async operation for unloading the AssetBundle.</param>
        public void Start(ProvideHandle provideHandle, AssetBundleUnloadOperation unloadOp)
#else
        /// <summary>
        /// Stores AssetBundle loading information, starts loading the bundle.
        /// </summary>
        /// <param name="provideHandle">The container for information regarding loading the AssetBundle.</param>
        public void Start(ProvideHandle provideHandle)
#endif
        {
            m_Retries = 0;
            m_AssetBundle = null;
            m_RequestOperation = null;
            m_RequestCompletedCallbackCalled = false;
            m_ProvideHandle = provideHandle;
            m_ProvideHandle.SetWaitForCompletionCallback(WaitForCompletionHandler);
#if UNLOAD_BUNDLE_ASYNC
            m_UnloadOperation = unloadOp;
            if (m_UnloadOperation != null && !m_UnloadOperation.isDone)
                m_UnloadOperation.completed += OnUnloadOperationComplete;
            else
#endif
            BeginOperation();
        }

        private bool WaitForCompletionHandler()
        {
#if UNLOAD_BUNDLE_ASYNC
            if (m_UnloadOperation != null && !m_UnloadOperation.isDone)
            {
                m_UnloadOperation.completed -= OnUnloadOperationComplete;
                m_UnloadOperation.WaitForCompletion();
                m_UnloadOperation = null;
                BeginOperation();
            }
#endif

            if (m_RequestOperation == null)
            {
                return false;
            }

            if (!m_Completed) {

                // we don't have to check for done with local files as calling
                // m_requestOperation.assetBundle is blocking and will wait for the file to load
                if (!m_RequestCompletedCallbackCalled)
                {
                    m_RequestOperation.completed -= LocalRequestOperationCompleted;
                    LocalRequestOperationCompleted(m_RequestOperation);
                }
            }

            if (!m_Completed && m_RequestOperation.isDone)
            {
                m_ProvideHandle.Complete(this, m_AssetBundle != null, null);
                m_Completed = true;
            }

            return m_Completed;
        }

        void AddCallbackInvokeIfDone(AsyncOperation operation, Action<AsyncOperation> callback)
        {
            if (operation.isDone)
                callback(operation);
            else
                operation.completed += callback;
        }

        private void BeginOperation()
        {
            m_TransformedInternalId = m_ProvideHandle.Location.InternalId;

#if DEBUG
            Debug.Log("[AssetBundleResource] Load AssetBundle: " + m_TransformedInternalId);
#endif

            var assetBundlePath = ResourcePath.GetAssetBundleLoadPath(m_TransformedInternalId);
            m_RequestOperation = AssetBundle.LoadFromFileAsync(assetBundlePath);

#if ENABLE_ADDRESSABLE_PROFILER
            AddBundleToProfiler(Profiling.ContentStatus.Loading, m_Source);
#endif
            AddCallbackInvokeIfDone(m_RequestOperation, LocalRequestOperationCompleted);
        }

        private void LocalRequestOperationCompleted(AsyncOperation op)
        {
            if (m_RequestCompletedCallbackCalled)
            {
                return;
            }

            m_RequestCompletedCallbackCalled = true;

            m_AssetBundle = (op as AssetBundleCreateRequest).assetBundle;
#if ENABLE_ADDRESSABLE_PROFILER
            AddBundleToProfiler(Profiling.ContentStatus.Active, m_Source);
#endif

            if (m_AssetBundle != null)
            {
#if DEBUG
                Debug.Log("[AssetBundleResource] Load AssetBundle: " + m_TransformedInternalId + " Succeeded");
#endif
                m_ProvideHandle.Complete(this, true, null);
            }
            else
            {
                m_ProvideHandle.Complete<AssetBundleResource>(null, false,
                    new ProviderException(string.Format("Invalid path in AssetBundleProvider: '{0}'.", m_TransformedInternalId), m_ProvideHandle.Location));
            }

            m_Completed = true;
        }

#if UNLOAD_BUNDLE_ASYNC
        /// <summary>
        /// Starts an async operation that unloads all resources associated with the AssetBundle.
        /// </summary>
        /// <param name="unloadOp">The async operation.</param>
        /// <returns>Returns true if the async operation object is valid.</returns>
        public bool Unload(out AssetBundleUnloadOperation unloadOp)
#else
        /// <summary>
        /// Unloads all resources associated with the AssetBundle.
        /// </summary>
        public void Unload()
#endif
        {
#if UNLOAD_BUNDLE_ASYNC
            unloadOp = null;
            if (m_AssetBundle != null)
            {
                unloadOp = m_AssetBundle.UnloadAsync(true);
                m_AssetBundle = null;
            }
#else
            if (m_AssetBundle != null)
            {
                m_AssetBundle.Unload(true);
                m_AssetBundle = null;
            }
#endif
            m_RequestOperation = null;
#if ENABLE_ADDRESSABLE_PROFILER
            RemoveBundleFromProfiler();
#endif
#if UNLOAD_BUNDLE_ASYNC
            return unloadOp != null;
#endif
        }
    }

    /// <summary>
    /// IResourceProvider for asset bundles.  Loads bundles via UnityWebRequestAssetBundle API if the internalId starts with "http".  If not, it will load the bundle via AssetBundle.LoadFromFileAsync.
    /// </summary>
    [DisplayName("AssetBundle Provider")]
    public class AssetBundleProvider : ResourceProviderBase
    {
#if UNLOAD_BUNDLE_ASYNC
        private static Dictionary<string, AssetBundleUnloadOperation> m_UnloadingBundles = new Dictionary<string, AssetBundleUnloadOperation>();
        /// <summary>
        /// Stores async operations that unload the requested AssetBundles.
        /// </summary>
        protected internal static Dictionary<string, AssetBundleUnloadOperation> UnloadingBundles
        {
            get { return m_UnloadingBundles; }
            internal set { m_UnloadingBundles = value; }
        }

        internal static int UnloadingAssetBundleCount => m_UnloadingBundles.Count;
        internal static int AssetBundleCount => AssetBundle.GetAllLoadedAssetBundles().Count() - UnloadingAssetBundleCount;
        internal static void WaitForAllUnloadingBundlesToComplete()
        {
            if (UnloadingAssetBundleCount > 0)
            {
                var bundles = m_UnloadingBundles.Values.ToArray();
                foreach (var b in bundles)
                    b.WaitForCompletion();
            }
        }

#else
        internal static void WaitForAllUnloadingBundlesToComplete()
        {
        }
#endif

        /// <inheritdoc/>
        public override void Provide(ProvideHandle providerInterface)
        {
#if UNLOAD_BUNDLE_ASYNC
            if (m_UnloadingBundles.TryGetValue(providerInterface.Location.InternalId, out var unloadOp))
            {
                if (unloadOp.isDone)
                    unloadOp = null;
            }
            new AssetBundleResource().Start(providerInterface, unloadOp);
#else
            new AssetBundleResource().Start(providerInterface);
#endif
        }

        /// <inheritdoc/>
        public override Type GetDefaultType(IResourceLocation location)
        {
            return typeof(IAssetBundleResource);
        }

        /// <summary>
        /// Releases the asset bundle via AssetBundle.Unload(true).
        /// </summary>
        /// <param name="location">The location of the asset to release</param>
        /// <param name="asset">The asset in question</param>
        public override void Release(IResourceLocation location, object asset)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            if (asset == null)
            {
                Debug.LogWarningFormat("Releasing null asset bundle from location {0}.  This is an indication that the bundle failed to load.", location);
                return;
            }

            var bundle = asset as AssetBundleResource;
            if (bundle != null)
            {
#if UNLOAD_BUNDLE_ASYNC
                if (bundle.Unload(out var unloadOp))
                {
                    m_UnloadingBundles.Add(location.InternalId, unloadOp);
                    unloadOp.completed += op => m_UnloadingBundles.Remove(location.InternalId);
                }
#else
                bundle.Unload();
#endif
                return;
            }
        }
    }
}
