#if UNITY_2022_1_OR_NEWER
#define UNLOAD_BUNDLE_ASYNC
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Exceptions;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Serialization;


namespace UnityEngine.ResourceManagement.ResourceProviders
{
    internal class DownloadOnlyLocation : LocationWrapper
    {
        public DownloadOnlyLocation(IResourceLocation location) : base(location)
        {
        }
    }

    /// <summary>
    /// Used to indication how Assets are loaded from the AssetBundle on the first load request.
    /// </summary>
    public enum AssetLoadMode
    {
        /// <summary>
        /// Only load the requested Asset and Dependencies
        /// </summary>
        RequestedAssetAndDependencies = 0,

        /// <summary>
        /// Load all assets inside the AssetBundle
        /// </summary>
        AllPackedAssetsAndDependencies,
    }

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
    /// Contains cache information to be used by the AssetBundleProvider
    /// </summary>
    [Serializable]
    public class AssetBundleRequestOptions : ILocationSizeData
    {
        [FormerlySerializedAs("m_hash")]
        [SerializeField]
        string m_Hash = "";

        /// <summary>
        /// Hash value of the asset bundle.
        /// </summary>
        public string Hash
        {
            get { return m_Hash; }
            set { m_Hash = value; }
        }

        [SerializeField]
        string m_BundleName = null;

        /// <summary>
        /// The name of the original bundle.  This does not contain the appended hash.
        /// </summary>
        public string BundleName
        {
            get { return m_BundleName; }
            set { m_BundleName = value; }
        }

        [SerializeField]
        AssetLoadMode m_AssetLoadMode = AssetLoadMode.RequestedAssetAndDependencies;

        /// <summary>
        /// Determines how Assets are loaded when accessed.
        /// </summary>
        /// <remarks>
        /// Requested Asset And Dependencies, will only load the requested Asset (Recommended).
        /// All Packed Assets And Dependencies, will load all Assets that are packed together. Best used when loading all Assets into memory is required.
        ///</remarks>
        public AssetLoadMode AssetLoadMode
        {
            get { return m_AssetLoadMode; }
            set { m_AssetLoadMode = value; }
        }

        [SerializeField]
        long m_BundleSize;

        /// <summary>
        /// The size of the bundle, in bytes.
        /// </summary>
        public long BundleSize
        {
            get { return m_BundleSize; }
            set { m_BundleSize = value; }
        }

        /// <summary>
        /// Computes the amount of data needed to be downloaded for this bundle.
        /// </summary>
        /// <param name="location">The location of the bundle.</param>
        /// <param name="resourceManager">The object that contains all the resource locations.</param>
        /// <returns>The size in bytes of the bundle that is needed to be downloaded.  If the local cache contains the bundle or it is a local bundle, 0 will be returned.</returns>
        public virtual long ComputeSize(IResourceLocation location, ResourceManager resourceManager)
        {
            return 0;
        }
    }

    /// <summary>
    /// Provides methods for loading an AssetBundle from a local or remote location.
    /// </summary>
    public class AssetBundleResource : IAssetBundleResource
    {
        AssetBundle m_AssetBundle;
        AsyncOperation m_RequestOperation;
        internal ProvideHandle m_ProvideHandle;
        internal AssetBundleRequestOptions m_Options;

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

            if (m_Options.AssetLoadMode == AssetLoadMode.AllPackedAssetsAndDependencies)
            {
#if !UNITY_2021_1_OR_NEWER
                if (AsyncOperationHandle.IsWaitingForCompletion)
                {
                    m_AssetBundle.LoadAllAssets();
                    m_PreloadCompleted = true;
                    return null;
                }
#endif
                if (m_PreloadRequest == null)
                {
                    m_PreloadRequest = m_AssetBundle.LoadAllAssetsAsync();
                    m_PreloadRequest.completed += operation => m_PreloadCompleted = true;
                }

                return m_PreloadRequest;
            }

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
            m_Options = m_ProvideHandle.Location.Data as AssetBundleRequestOptions;
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

        /// <summary>
        /// Determines where an AssetBundle can be loaded from.
        /// </summary>
        /// <param name="handle">The container for AssetBundle loading information.</param>
        /// <param name="loadType">Specifies where an AssetBundle can be loaded from.</param>
        /// <param name="path">The file path or url where the AssetBundle is located.</param>
        public static void GetLoadInfo(ProvideHandle handle, out string path)
        {
            GetLoadInfo(handle.Location, handle.ResourceManager, out path);
        }

        internal static void GetLoadInfo(IResourceLocation location, ResourceManager resourceManager, out string path)
        {
            var options = location?.Data as AssetBundleRequestOptions;
            if (options == null)
            {
                path = null;
                return;
            }

            path = resourceManager.TransformInternalId(location);
            if (Application.platform == RuntimePlatform.Android && path.StartsWith("jar:", StringComparison.Ordinal))
                return;
            if (ResourceManagerConfig.ShouldPathUseWebRequest(path))
                throw new NotSupportedException();
        }

        private void BeginOperation()
        {
            GetLoadInfo(m_ProvideHandle, out m_TransformedInternalId);

            {
#if !UNITY_2021_1_OR_NEWER
                if (AsyncOperationHandle.IsWaitingForCompletion)
                    CompleteBundleLoad(AssetBundle.LoadFromFile(m_TransformedInternalId, m_Options == null ? 0 : m_Options.Crc));
                else
#endif
                {
                    m_RequestOperation = AssetBundle.LoadFromFileAsync(m_TransformedInternalId);
#if ENABLE_ADDRESSABLE_PROFILER
                    AddBundleToProfiler(Profiling.ContentStatus.Loading, m_Source);
#endif
                    AddCallbackInvokeIfDone(m_RequestOperation, LocalRequestOperationCompleted);
                }
            }
        }

        private void LocalRequestOperationCompleted(AsyncOperation op)
        {
            if (m_RequestCompletedCallbackCalled)
            {
                return;
            }

            m_RequestCompletedCallbackCalled = true;
            CompleteBundleLoad((op as AssetBundleCreateRequest).assetBundle);
        }

        private void CompleteBundleLoad(AssetBundle bundle)
        {
            m_AssetBundle = bundle;
#if ENABLE_ADDRESSABLE_PROFILER
            AddBundleToProfiler(Profiling.ContentStatus.Active, m_Source);
#endif
            if (m_AssetBundle != null)
                m_ProvideHandle.Complete(this, true, null);
            else
                m_ProvideHandle.Complete<AssetBundleResource>(null, false,
                    new ProviderException(string.Format("Invalid path in AssetBundleProvider: '{0}'.", m_TransformedInternalId), m_ProvideHandle.Location));
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
