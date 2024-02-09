using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Assertions;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets
{
    internal class AddressablesImpl : IEqualityComparer<IResourceLocation>
    {
        ResourceManager m_ResourceManager;

        public readonly IInstanceProvider InstanceProvider = new InstanceProvider();
        public readonly ISceneProvider SceneProvider = new SceneProvider();

        public ResourceManager ResourceManager
        {
            get
            {
                if (m_ResourceManager == null)
                    m_ResourceManager = new ResourceManager(new DefaultAllocationStrategy());
                return m_ResourceManager;
            }
        }

        [CanBeNull]
        internal IResourceLocator m_ResourceLocator;

        Action<AsyncOperationHandle> m_OnHandleCompleteAction;
        Action<AsyncOperationHandle> m_OnSceneHandleCompleteAction;
        Action<AsyncOperationHandle> m_OnHandleDestroyedAction;
        Dictionary<object, AsyncOperationHandle> m_resultToHandle = new();
        internal HashSet<AsyncOperationHandle> m_SceneInstances = new();

        internal int SceneOperationCount
        {
            get { return m_SceneInstances.Count; }
        }

        internal int TrackedHandleCount
        {
            get { return m_resultToHandle.Count; }
        }

        internal bool initialized = false;

        public AddressablesImpl(IAllocationStrategy alloc)
        {
            m_ResourceManager = new ResourceManager(alloc);
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        internal void ReleaseSceneManagerOperation()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        internal void OnSceneUnloaded(Scene scene)
        {
            foreach (var s in m_SceneInstances)
            {
                if (!s.IsValid())
                {
                    m_SceneInstances.Remove(s);
                    break;
                }

                var sceneHandle = s.Convert<SceneInstance>();
                if (sceneHandle.Result.Scene == scene)
                {
                    m_SceneInstances.Remove(s);
                    m_resultToHandle.Remove(s.Result);

                    var op = SceneProvider.ReleaseScene(m_ResourceManager, sceneHandle);
                    AutoReleaseHandleOnCompletion(op);
                    break;
                }
            }

            m_ResourceManager.CleanupSceneInstances(scene);
        }

        public void SetResourceLocator(IResourceLocator loc) => m_ResourceLocator = loc;

        internal bool GetResourceLocation(string key, Type type, out IResourceLocation location)
        {
            return m_ResourceLocator!.Locate(key, type, out location);
        }

        void Initialize()
        {
            if (initialized)
                return;

            initialized = true;

            ResourceManager.ExceptionHandler ??= L.Exception;

            //these need to be referenced in order to prevent stripping on IL2CPP platforms.
            GC.KeepAlive(Application.streamingAssetsPath);
#if !UNITY_SWITCH
            GC.KeepAlive(Application.persistentDataPath);
#endif

            m_OnHandleCompleteAction = OnHandleCompleted;
            m_OnSceneHandleCompleteAction = OnSceneHandleCompleted;
            m_OnHandleDestroyedAction = OnHandleDestroyed;

#if UNITY_EDITOR
            //this indicates that a specific addressables settings asset is being used for the runtime locations
            if (AddressablesEditorInitializer.InitializeOverride is not null)
            {
                AddressablesEditorInitializer.InitializeOverride(this);
            }
            else
#endif
            {
                Initializer.Initialize(this, out var error);
                if (error != null)
                    L.E(error);
            }
        }

        [Conditional("UNITY_EDITOR")]
        void QueueEditorUpdateIfNeeded()
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        AsyncOperationHandle<SceneInstance> TrackHandle(AsyncOperationHandle<SceneInstance> handle)
        {
            handle.Completed += (sceneHandle) => { m_OnSceneHandleCompleteAction(sceneHandle); };
            return handle;
        }

        AsyncOperationHandle<TObject> TrackHandle<TObject>(AsyncOperationHandle<TObject> handle)
        {
            handle.CompletedTypeless += m_OnHandleCompleteAction;
            return handle;
        }

        AsyncOperationHandle TrackHandle(AsyncOperationHandle handle)
        {
            handle.Completed += m_OnHandleCompleteAction;
            return handle;
        }

        internal void ClearTrackHandles()
        {
            m_resultToHandle.Clear();
        }

        public AsyncOperationHandle<TObject> LoadAssetAsync<TObject>(IResourceLocation location)
        {
            QueueEditorUpdateIfNeeded();
            if (initialized is false) Initialize();
            return TrackHandle(ResourceManager.ProvideResource<TObject>(location));
        }

        public AsyncOperationHandle<TObject> LoadAssetAsync<TObject>(string key)
        {
            Assert.IsNotNull(key, "Cannot load asset with null key");

            QueueEditorUpdateIfNeeded();
            if (initialized is false) Initialize();

            var t = typeof(TObject);
            if (t.IsArray)
                t = t.GetElementType();
            else if (t.IsGenericType && typeof(IList<>) == t.GetGenericTypeDefinition())
                t = t.GetGenericArguments()[0];

            var locator = m_ResourceLocator;
            Assert.IsNotNull(locator, "Addressables.m_ResourceLocator is null");
            if (locator.Locate(key, t, out var loc))
                return TrackHandle(ResourceManager.ProvideResource<TObject>(loc));

            return ResourceManager.CreateCompletedOperationWithException<TObject>(default(TObject), new InvalidKeyException(key, t, this));
        }

        void OnHandleDestroyed(AsyncOperationHandle handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                m_resultToHandle.Remove(handle.Result);
            }
        }

        void OnSceneHandleCompleted(AsyncOperationHandle handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                m_SceneInstances.Add(handle);
                if (!m_resultToHandle.ContainsKey(handle.Result))
                {
                    handle.Destroyed += m_OnHandleDestroyedAction;
                    m_resultToHandle.Add(handle.Result, handle);
                }
            }
        }

        void OnHandleCompleted(AsyncOperationHandle handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                if (!m_resultToHandle.ContainsKey(handle.Result))
                {
                    handle.Destroyed += m_OnHandleDestroyedAction;
                    m_resultToHandle.Add(handle.Result, handle);
                }
            }
        }

        public void Release<TObject>(TObject obj)
        {
            if (obj == null)
            {
                L.W("Addressables.Release() - trying to release null object.");
                return;
            }

            AsyncOperationHandle handle;
            if (m_resultToHandle.TryGetValue(obj, out handle))
                Release(handle);
            else
            {
                L.E("Addressables.Release was called on an object that Addressables was not previously aware of.  Thus nothing is being released");
            }
        }

        public void Release<TObject>(AsyncOperationHandle<TObject> handle)
        {
            if (typeof(TObject) == typeof(SceneInstance))
            {
                SceneInstance sceneInstance = (SceneInstance)Convert.ChangeType(handle.Result, typeof(SceneInstance));
                if (sceneInstance.Scene.isLoaded && handle.ReferenceCount == 1)
                {
                    if (SceneOperationCount == 1 && m_SceneInstances.First().Equals(handle))
                        m_SceneInstances.Clear();
                    UnloadSceneAsync(handle, UnloadSceneOptions.None, true);
                }
                else if (!sceneInstance.Scene.isLoaded && handle.ReferenceCount == 2 && !handle.UnloadSceneOpExcludeReleaseCallback)
                {
                    AutoReleaseHandleOnCompletion(handle);
                }
            }

            m_ResourceManager.Release(handle);
        }

        public void Release(AsyncOperationHandle handle)
        {
            m_ResourceManager.Release(handle);
        }

        internal void AutoReleaseHandleOnCompletion(AsyncOperationHandle handle)
        {
            handle.Completed += op => Release(op);
        }

        internal void AutoReleaseHandleOnCompletion<TObject>(AsyncOperationHandle<TObject> handle)
        {
            handle.Completed += op => Release(op);
        }

        internal void AutoReleaseHandleOnCompletion<TObject>(AsyncOperationHandle<TObject> handle, bool unloadSceneOpExcludeReleaseCallback)
        {
            handle.Completed += op =>
            {
                if (unloadSceneOpExcludeReleaseCallback)
                    op.UnloadSceneOpExcludeReleaseCallback = true;
                Release(op);
            };
        }

        internal void AutoReleaseHandleOnTypelessCompletion<TObject>(AsyncOperationHandle<TObject> handle)
        {
            handle.CompletedTypeless += op => Release(op);
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, Transform parent = null, bool instantiateInWorldSpace = false, bool trackHandle = true)
        {
            return InstantiateAsync(location, new InstantiationParameters(parent, instantiateInWorldSpace), trackHandle);
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, Vector3 position, Quaternion rotation, Transform parent = null, bool trackHandle = true)
        {
            return InstantiateAsync(location, new InstantiationParameters(position, rotation, parent), trackHandle);
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(string key, Transform parent = null, bool instantiateInWorldSpace = false, bool trackHandle = true)
        {
            return InstantiateAsync(key, new InstantiationParameters(parent, instantiateInWorldSpace), trackHandle);
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(string key, Vector3 position, Quaternion rotation, Transform parent = null, bool trackHandle = true)
        {
            return InstantiateAsync(key, new InstantiationParameters(position, rotation, parent), trackHandle);
        }

        AsyncOperationHandle<GameObject> InstantiateWithChain(AsyncOperationHandle dep, string key, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            var chainOp = ResourceManager.CreateChainOperation(dep, op => InstantiateAsync(key, instantiateParameters, false));
            if (trackHandle)
                chainOp.CompletedTypeless += m_OnHandleCompleteAction;
            return chainOp;
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(string key, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            QueueEditorUpdateIfNeeded();
            if (initialized is false) Initialize();

            var locator = m_ResourceLocator;
            if (locator!.Locate(key, typeof(GameObject), out var loc))
                return InstantiateAsync(loc, instantiateParameters, trackHandle);

            return ResourceManager.CreateCompletedOperationWithException<GameObject>(null, new InvalidKeyException(key, typeof(GameObject), this));
        }

        AsyncOperationHandle<GameObject> InstantiateWithChain(AsyncOperationHandle dep, IResourceLocation location, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            var chainOp = ResourceManager.CreateChainOperation(dep, op => InstantiateAsync(location, instantiateParameters, false));
            if (trackHandle)
                chainOp.CompletedTypeless += m_OnHandleCompleteAction;
            return chainOp;
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            QueueEditorUpdateIfNeeded();
            if (initialized is false) Initialize();

            var opHandle = ResourceManager.ProvideInstance(InstanceProvider, location, instantiateParameters);
            if (!trackHandle)
                return opHandle;
            opHandle.CompletedTypeless += m_OnHandleCompleteAction;
            return opHandle;
        }

        public bool ReleaseInstance(GameObject instance)
        {
            if (instance == null)
            {
                L.W("Addressables.ReleaseInstance() - trying to release null object.");
                return false;
            }

            AsyncOperationHandle handle;
            if (m_resultToHandle.TryGetValue(instance, out handle))
                Release(handle);
            else
                return false;

            return true;
        }

        internal AsyncOperationHandle<SceneInstance> LoadSceneWithChain(AsyncOperationHandle dep, string key, LoadSceneMode loadMode, bool activateOnLoad = true, int priority = 100)
        {
            return TrackHandle(ResourceManager.CreateChainOperation(dep, op => LoadSceneAsync(key, loadMode, activateOnLoad, priority, false)));
        }

        internal AsyncOperationHandle<SceneInstance> LoadSceneWithChain(AsyncOperationHandle dep, IResourceLocation key, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true,
            int priority = 100)
        {
            return TrackHandle(ResourceManager.CreateChainOperation(dep, op => LoadSceneAsync(key, loadMode, activateOnLoad, priority, false)));
        }

        public AsyncOperationHandle<SceneInstance> LoadSceneAsync(string key)
        {
            return LoadSceneAsync(key, LoadSceneMode.Single);
        }

        public AsyncOperationHandle<SceneInstance> LoadSceneAsync(string key, LoadSceneMode loadMode, bool activateOnLoad = true, int priority = 100, bool trackHandle = true)
        {
            QueueEditorUpdateIfNeeded();
            if (initialized is false) Initialize();

            if (!GetResourceLocation(key, typeof(SceneInstance), out var location))
                return ResourceManager.CreateCompletedOperationWithException<SceneInstance>(default(SceneInstance), new InvalidKeyException(key, typeof(SceneInstance), this));

            return LoadSceneAsync(location, loadMode, activateOnLoad, priority, trackHandle);
        }

        public AsyncOperationHandle<SceneInstance> LoadSceneAsync(IResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100,
            bool trackHandle = true)
        {
            QueueEditorUpdateIfNeeded();
            if (initialized is false) Initialize();

            var handle = ResourceManager.ProvideScene(SceneProvider, location, loadMode, activateOnLoad, priority);
            if (trackHandle)
                return TrackHandle(handle);

            return handle;
        }

        public AsyncOperationHandle<SceneInstance> UnloadSceneAsync(SceneInstance scene, UnloadSceneOptions unloadOptions = UnloadSceneOptions.None, bool autoReleaseHandle = true)
        {
            AsyncOperationHandle handle;
            if (!m_resultToHandle.TryGetValue(scene, out handle))
            {
                var msg = string.Format("Addressables.UnloadSceneAsync() - Cannot find handle for scene {0}", scene);
                L.W(msg);
                return ResourceManager.CreateCompletedOperation<SceneInstance>(scene, msg);
            }

            if (handle.m_InternalOp.IsRunning)
                return CreateUnloadSceneWithChain(handle, unloadOptions, autoReleaseHandle);

            return UnloadSceneAsync(handle, unloadOptions, autoReleaseHandle);
        }

        public AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle handle, UnloadSceneOptions unloadOptions = UnloadSceneOptions.None, bool autoReleaseHandle = true)
        {
            QueueEditorUpdateIfNeeded();
            if (handle.m_InternalOp.IsRunning)
                return CreateUnloadSceneWithChain(handle, unloadOptions, autoReleaseHandle);

            return UnloadSceneAsync(handle.Convert<SceneInstance>(), unloadOptions, autoReleaseHandle);
        }

        public AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle<SceneInstance> handle, UnloadSceneOptions unloadOptions = UnloadSceneOptions.None,
            bool autoReleaseHandle = true)
        {
            if (handle.m_InternalOp.IsRunning)
                return CreateUnloadSceneWithChain(handle, unloadOptions, autoReleaseHandle);

            return InternalUnloadScene(handle, unloadOptions, autoReleaseHandle);
        }

        internal AsyncOperationHandle<SceneInstance> CreateUnloadSceneWithChain(AsyncOperationHandle handle, UnloadSceneOptions unloadOptions, bool autoReleaseHandle)
        {
            return m_ResourceManager.CreateChainOperation(handle, (completedHandle) => InternalUnloadScene(completedHandle.Convert<SceneInstance>(), unloadOptions, autoReleaseHandle));
        }

        internal AsyncOperationHandle<SceneInstance> CreateUnloadSceneWithChain(AsyncOperationHandle<SceneInstance> handle, UnloadSceneOptions unloadOptions, bool autoReleaseHandle)
        {
            return m_ResourceManager.CreateChainOperation(handle, (completedHandle) => InternalUnloadScene(completedHandle, unloadOptions, autoReleaseHandle));
        }

        internal AsyncOperationHandle<SceneInstance> InternalUnloadScene(AsyncOperationHandle<SceneInstance> handle, UnloadSceneOptions unloadOptions, bool autoReleaseHandle)
        {
            QueueEditorUpdateIfNeeded();
            var relOp = SceneProvider.ReleaseScene(ResourceManager, handle, unloadOptions);
            if (autoReleaseHandle)
                AutoReleaseHandleOnCompletion(relOp, true);
            return relOp;
        }

        //needed for IEqualityComparer<IResourceLocation> interface
        public bool Equals(IResourceLocation x, IResourceLocation y)
        {
            return x.PrimaryKey.Equals(y.PrimaryKey) && x.ResourceType.Equals(y.ResourceType) && x.InternalId.Equals(y.InternalId);
        }

        //needed for IEqualityComparer<IResourceLocation> interface
        public int GetHashCode(IResourceLocation loc)
        {
            return loc.PrimaryKey.GetHashCode() * 31 + loc.ResourceType.GetHashCode();
        }
    }
}
