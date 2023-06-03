#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Exceptions;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Serialization;

namespace UnityEngine.ResourceManagement.ResourceProviders.Simulation
{
    abstract class VBAsyncOperation
    {
    }

    class VBAsyncOperation<TObject> : VBAsyncOperation
    {
        protected TObject m_Result;
        protected AsyncOperationStatus m_Status;
        protected Exception m_Error;
        protected object m_Context;

        DelegateList<VBAsyncOperation<TObject>> m_CompletedAction;
        Action<VBAsyncOperation<TObject>> m_OnDestroyAction;

        public virtual bool WaitForCompletion() => true;

        public override string ToString()
        {
            var instId = "";
            var or = m_Result as Object;
            if (or != null)
                instId = "(" + or.GetInstanceID() + ")";
            return string.Format("{0}, result='{1}', status='{2}', location={3}.", base.ToString(), (m_Result + instId), m_Status, m_Context);
        }

        public event Action<VBAsyncOperation<TObject>> Completed
        {
            add
            {
                if (IsDone)
                {
                    DelayedActionManager.AddAction(value, 0, this);
                }
                else
                {
                    if (m_CompletedAction == null)
                        m_CompletedAction = DelegateList<VBAsyncOperation<TObject>>.CreateWithGlobalCache();
                    m_CompletedAction.Add(value);
                }
            }

            remove { m_CompletedAction.Remove(value); }
        }

        public AsyncOperationStatus Status
        {
            get { return m_Status; }
            protected set { m_Status = value; }
        }

        /// <inheritdoc />
        public Exception OperationException
        {
            get { return m_Error; }
            protected set
            {
                m_Error = value;
                if (m_Error != null && ResourceManager.ExceptionHandler != null)
                    ResourceManager.ExceptionHandler(new AsyncOperationHandle(null), value);
            }
        }

        public TObject Result
        {
            get { return m_Result; }
        }

        public virtual bool IsDone
        {
            get { return Status == AsyncOperationStatus.Failed || Status == AsyncOperationStatus.Succeeded; }
        }

        /// <inheritdoc />
        public object Context
        {
            get { return m_Context; }
            set { m_Context = value; }
        }

        public void InvokeCompletionEvent()
        {
            if (m_CompletedAction != null)
            {
                m_CompletedAction.Invoke(this);
                m_CompletedAction.Clear();
            }
        }


        public virtual void SetResult(TObject result)
        {
            m_Result = result;
            m_Status = (m_Result == null) ? AsyncOperationStatus.Failed : AsyncOperationStatus.Succeeded;
        }

        public VBAsyncOperation<TObject> StartCompleted(object context, object key, TObject val, Exception error = null)
        {
            Context = context;
            OperationException = error;
            m_Result = val;
            m_Status = (m_Result == null) ? AsyncOperationStatus.Failed : AsyncOperationStatus.Succeeded;
            DelayedActionManager.AddAction((Action)InvokeCompletionEvent);
            return this;
        }
    }


    /// <summary>
    /// Contains data needed to simulate a bundled asset
    /// </summary>
    [Serializable]
    public class VirtualAssetBundleEntry
    {
        [FormerlySerializedAs("m_name")]
        [SerializeField]
        string m_Name;

        /// <summary>
        /// The name of the asset.
        /// </summary>
        public string Name
        {
            get { return m_Name; }
        }

        [FormerlySerializedAs("m_size")]
        [SerializeField]
        long m_Size;

        /// <summary>
        /// The file size of the asset, in bytes.
        /// </summary>
        public long Size
        {
            get { return m_Size; }
        }

        [SerializeField]
        internal string m_AssetPath;

        /// <summary>
        /// Construct a new VirtualAssetBundleEntry
        /// </summary>
        public VirtualAssetBundleEntry()
        {
        }

        /// <summary>
        /// Construct a new VirtualAssetBundleEntry
        /// </summary>
        /// <param name="name">The name of the asset.</param>
        /// <param name="size">The size of the asset, in bytes.</param>
        public VirtualAssetBundleEntry(string name, long size)
        {
            m_Name = name;
            m_Size = size;
        }
    }

    /// <summary>
    /// Contains data need to simulate an asset bundle.
    /// </summary>
    [Serializable]
    public class VirtualAssetBundle : ISerializationCallbackReceiver, IAssetBundleResource
    {
        [FormerlySerializedAs("m_name")]
        [SerializeField]
        string m_Name;

        [FormerlySerializedAs("m_dataSize")]
        [SerializeField]
        long m_DataSize;

        [FormerlySerializedAs("m_headerSize")]
        [SerializeField]
        long m_HeaderSize;

        [FormerlySerializedAs("m_latency")]
        [SerializeField]
        float m_Latency;

        [SerializeField]
        uint m_Crc;

        [SerializeField]
        string m_Hash;

        [FormerlySerializedAs("m_serializedAssets")]
        [SerializeField]
        List<VirtualAssetBundleEntry> m_SerializedAssets = new List<VirtualAssetBundleEntry>();

        long m_HeaderBytesLoaded;
        long m_DataBytesLoaded;

        LoadAssetBundleOp m_BundleLoadOperation;
        List<IVirtualLoadable> m_AssetLoadOperations = new List<IVirtualLoadable>();
        Dictionary<string, VirtualAssetBundleEntry> m_AssetMap;

        /// <summary>
        /// The name of the bundle.
        /// </summary>
        public string Name
        {
            get { return m_Name; }
        }

        /// <summary>
        /// The assets contained in the bundle.
        /// </summary>
        public List<VirtualAssetBundleEntry> Assets
        {
            get { return m_SerializedAssets; }
        }

        const long k_SynchronousBytesPerSecond = (long)1024 * 1024 * 1024 * 10; // 10 Gb/s

        /// <summary>
        /// Construct a new VirtualAssetBundle object.
        /// </summary>
        public VirtualAssetBundle()
        {
        }

        /// <summary>
        /// Construct a new VirtualAssetBundle
        /// </summary>
        /// <param name="name">The name of the bundle.</param>
        /// <param name="local">Is the bundle local or remote.  This is used to determine which bandwidth value to use when simulating loading.</param>
        public VirtualAssetBundle(string name, uint crc, string hash)
        {
            m_Latency = .1f;
            m_Name = name;
            m_HeaderBytesLoaded = 0;
            m_DataBytesLoaded = 0;
            m_Crc = crc;
            m_Hash = hash;
        }

        /// <summary>
        /// Set the size of the bundle.
        /// </summary>
        /// <param name="dataSize">The size of the data.</param>
        /// <param name="headerSize">The size of the header.</param>
        public void SetSize(long dataSize, long headerSize)
        {
            m_HeaderSize = headerSize;
            m_DataSize = dataSize;
        }

        /// <summary>
        /// Not used
        /// </summary>
        public void OnBeforeSerialize()
        {
        }

        /// <summary>
        /// Load serialized data into runtime structures.
        /// </summary>
        public void OnAfterDeserialize()
        {
            m_AssetMap = new Dictionary<string, VirtualAssetBundleEntry>();
            foreach (var a in m_SerializedAssets)
                m_AssetMap.Add(a.Name, a);
        }

        class LoadAssetBundleOp : VBAsyncOperation<VirtualAssetBundle>
        {
            VirtualAssetBundle m_Bundle;
            float m_TimeInLoadingState;

            public LoadAssetBundleOp(IResourceLocation location, VirtualAssetBundle bundle)
            {
                Context = location;
                m_Bundle = bundle;
                m_TimeInLoadingState = 0.0f;
            }

            public override bool WaitForCompletion()
            {
                SetResult(m_Bundle);
                InvokeCompletionEvent();
                return true;
            }

            public void Update(long localBandwidth, float unscaledDeltaTime)
            {
                if (m_Result != null)
                    return;

                m_TimeInLoadingState += unscaledDeltaTime;
                if (m_TimeInLoadingState > m_Bundle.m_Latency)
                {
                    long localBytes = (long)Math.Ceiling(unscaledDeltaTime * localBandwidth);

                    if (m_Bundle.LoadData(localBytes))
                    {
                        SetResult(m_Bundle);
                        InvokeCompletionEvent();
                    }
                }
            }
        }

        bool LoadData(long localBytes)
        {
            m_HeaderBytesLoaded += localBytes;
            if (m_HeaderBytesLoaded < m_HeaderSize)
                return false;
            m_HeaderBytesLoaded = m_HeaderSize;
            return true;
        }

        internal bool Unload()
        {
            if (m_BundleLoadOperation == null)
                Debug.LogWarningFormat("Simulated assetbundle {0} is already unloaded.", m_Name);
            m_HeaderBytesLoaded = 0;
            m_BundleLoadOperation = null;
            return true;
        }

        internal VBAsyncOperation<VirtualAssetBundle> StartLoad(IResourceLocation location)
        {
            if (m_BundleLoadOperation != null)
            {
                if (m_BundleLoadOperation.IsDone)
                    Debug.LogWarningFormat("Simulated assetbundle {0} is already loaded.", m_Name);
                else
                    Debug.LogWarningFormat("Simulated assetbundle {0} is already loading.", m_Name);
                return m_BundleLoadOperation;
            }

            m_HeaderBytesLoaded = 0;
            return (m_BundleLoadOperation = new LoadAssetBundleOp(location, this));
        }

        /// <summary>
        /// Load an asset via its location.  The asset will actually be loaded via the AssetDatabase API.
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <param name="location"></param>
        /// <returns></returns>
        internal VBAsyncOperation<object> LoadAssetAsync(ProvideHandle provideHandle, IResourceLocation location)
        {
            if (location == null)
                throw new ArgumentException("IResourceLocation location cannot be null.");
            if (m_BundleLoadOperation == null)
                return new VBAsyncOperation<object>().StartCompleted(location, location, null, new ResourceManagerException("LoadAssetAsync called on unloaded bundle " + m_Name));

            if (!m_BundleLoadOperation.IsDone)
                return new VBAsyncOperation<object>().StartCompleted(location, location, null, new ResourceManagerException("LoadAssetAsync called on loading bundle " + m_Name));
            VirtualAssetBundleEntry assetInfo;
            var assetPath = location.InternalId;
            if (ResourceManagerConfig.ExtractKeyAndSubKey(assetPath, out string mainPath, out string subKey))
                assetPath = mainPath;

            //this needs to use the non translated internal id since that was how the table was built.
            if (!m_AssetMap.TryGetValue(assetPath, out assetInfo))
                return new VBAsyncOperation<object>().StartCompleted(location, location, null,
                    new ResourceManagerException(string.Format("Unable to load asset {0} from simulated bundle {1}.", location.InternalId, Name)));

            var op = new LoadAssetOp(location, assetInfo, provideHandle);
            m_AssetLoadOperations.Add(op);
            return op;
        }

        internal void CountBandwidthUsage(ref long localCount)
        {
            if (m_BundleLoadOperation != null && m_BundleLoadOperation.IsDone)
            {
                localCount += m_AssetLoadOperations.Count;
                return;
            }

            localCount++;
        }

        interface IVirtualLoadable
        {
            bool Load(long localBandwidth, float unscaledDeltaTime);
        }

        // TODO: This is only needed internally. We can change this to not derive off of AsyncOperationBase and simplify the code
        class LoadAssetOp : VBAsyncOperation<object>, IVirtualLoadable
        {
            long m_BytesLoaded;
            float m_LastUpdateTime;
            VirtualAssetBundleEntry m_AssetInfo;
            ProvideHandle m_provideHandle;

            public LoadAssetOp(IResourceLocation location, VirtualAssetBundleEntry assetInfo, ProvideHandle ph)
            {
                m_provideHandle = ph;
                Context = location;
                m_AssetInfo = assetInfo;
                m_LastUpdateTime = Time.realtimeSinceStartup;
            }

            public override bool WaitForCompletion()
            {
                //TODO: this needs to just wait on the resourcemanager update loop to ensure proper loading order
                while (!IsDone)
                {
                    Load(k_SynchronousBytesPerSecond, .1f);
                    System.Threading.Thread.Sleep(100);
                }

                return true;
            }

            public bool Load(long localBandwidth, float unscaledDeltaTime)
            {
                if (IsDone)
                    return false;
                var now = m_LastUpdateTime + unscaledDeltaTime;
                if (now > m_LastUpdateTime)
                {
                    m_BytesLoaded += (long)Math.Ceiling((now - m_LastUpdateTime) * localBandwidth);
                    m_LastUpdateTime = now;
                }

                if (m_BytesLoaded < m_AssetInfo.Size)
                    return true;
                if (!(Context is IResourceLocation))
                    return false;
                var location = Context as IResourceLocation;
                var assetPath = m_AssetInfo.m_AssetPath;
                object result = null;

                var pt = m_provideHandle.Type;
                if (pt.IsArray)
                    result = ResourceManagerConfig.CreateArrayResult(pt, AssetDatabaseProvider.LoadAssetsWithSubAssets(assetPath));
                else if (pt.IsGenericType && typeof(IList<>) == pt.GetGenericTypeDefinition())
                    result = ResourceManagerConfig.CreateListResult(pt, AssetDatabaseProvider.LoadAssetsWithSubAssets(assetPath));
                else
                {
                    if (ResourceManagerConfig.ExtractKeyAndSubKey(location.InternalId, out string mainPath, out string subKey))
                        result = AssetDatabaseProvider.LoadAssetSubObject(assetPath, subKey, pt);
                    else
                        result = AssetDatabaseProvider.LoadAssetAtPath(assetPath, m_provideHandle);
                }

                SetResult(result);
                InvokeCompletionEvent();
                return false;
            }
        }

        //return true until complete
        internal bool UpdateAsyncOperations(long localBandwidth, float unscaledDeltaTime)
        {
            if (m_BundleLoadOperation == null)
                return false;

            if (!m_BundleLoadOperation.IsDone)
            {
                m_BundleLoadOperation.Update(localBandwidth, unscaledDeltaTime);
                return true;
            }

            foreach (var o in m_AssetLoadOperations)
            {
                if (!o.Load(localBandwidth, unscaledDeltaTime))
                {
                    m_AssetLoadOperations.Remove(o);
                    break;
                }
            }

            return m_AssetLoadOperations.Count > 0;
        }

        /// <summary>
        /// Implementation of IAssetBundleResource API
        /// </summary>
        /// <returns>Always returns null.</returns>
        public AssetBundle GetAssetBundle()
        {
            return null;
        }
    }
}
#endif
