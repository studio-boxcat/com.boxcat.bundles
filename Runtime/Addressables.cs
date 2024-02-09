using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Exception to encapsulate invalid key errors.
    /// </summary>
    public class InvalidKeyException : Exception
    {
        /// <summary>
        /// The key used to generate the exception.
        /// </summary>
        public object Key { get; private set; }

        /// <summary>
        /// The type of the key used to generate the exception.
        /// </summary>
        public Type Type { get; private set; }

        private AddressablesImpl m_Addressables;

        internal InvalidKeyException(string key, Type type, AddressablesImpl addr)
        {
            Key = key;
            Type = type;
            m_Addressables = addr;
        }

        const string BaseInvalidKeyMessageFormat = "{0}, Key={1}, Type={2}";

        /// <summary>
        /// Stores information about the exception.
        /// </summary>
        public override string Message
        {
            get
            {
                string stringKey = Key as string;
                if (!string.IsNullOrEmpty(stringKey))
                {
                    if (m_Addressables == null)
                        return string.Format(BaseInvalidKeyMessageFormat, base.Message, stringKey, Type);
                    return GetMessageForSingleKey(stringKey);
                }

                IEnumerable enumerableKey = Key as IEnumerable;
                if (enumerableKey != null)
                {
                    int keyCount = 0;
                    List<string> stringKeys = new List<string>();
                    HashSet<string> keyTypeNames = new HashSet<string>();
                    foreach (object keyObj in enumerableKey)
                    {
                        keyCount++;
                        keyTypeNames.Add(keyObj.GetType().ToString());
                        if (keyObj is string)
                            stringKeys.Add(keyObj as string);
                    }

                    string keysCSV = GetCSVString(stringKeys, "Key=", "Keys=");
                    return $"{base.Message} No MergeMode is set to merge the multiple keys requested. {keysCSV}, Type={Type}";
                }

                return string.Format(BaseInvalidKeyMessageFormat, base.Message, Key, Type);
            }
        }

        string GetMessageForSingleKey(string keyString)
        {
#if UNITY_EDITOR
            string path = AssetDatabase.GUIDToAssetPath(keyString);
            if (!string.IsNullOrEmpty(path))
            {
                Type directType = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (directType != null)
                    return $"{base.Message} Could not load Asset with GUID={keyString}, Path={path}. Asset exists with main Type={directType}, which is not assignable from the requested Type={Type}";
                return string.Format(BaseInvalidKeyMessageFormat, base.Message, keyString, Type);
            }
#endif

            return $"{base.Message} No Asset found with for Key={keyString}";
        }

        string GetCSVString(IEnumerable<string> enumerator, string prefixSingle, string prefixPlural)
        {
            StringBuilder keysCSVBuilder = new StringBuilder(prefixPlural);
            int count = 0;
            foreach (var key in enumerator)
            {
                count++;
                keysCSVBuilder.Append(count > 1 ? $", {key}" : key);
            }

            if (count == 1 && !string.IsNullOrEmpty(prefixPlural) && !string.IsNullOrEmpty(prefixSingle))
                keysCSVBuilder.Replace(prefixPlural, prefixSingle);
            return keysCSVBuilder.ToString();
        }
    }

    /// <summary>
    /// Entry point for Addressable API, this provides a simpler interface than using ResourceManager directly as it assumes string address type.
    /// </summary>
    public static class Addressables
    {
        internal static bool reinitializeAddressables = true;
        internal static AddressablesImpl m_AddressablesInstance = new AddressablesImpl(new LRUCacheAllocationStrategy(1000, 1000, 100, 10));

        static AddressablesImpl m_Addressables
        {
            get
            {
#if UNITY_EDITOR
                if (EditorSettings.enterPlayModeOptionsEnabled && reinitializeAddressables)
                {
                    L.I("[Addressables] Reinitializing Addressables");
                    reinitializeAddressables = false;
                    m_AddressablesInstance.ReleaseSceneManagerOperation();
                    m_AddressablesInstance = new AddressablesImpl(new LRUCacheAllocationStrategy(1000, 1000, 100, 10));
                }
#endif
                return m_AddressablesInstance;
            }
        }

        /// <summary>
        /// Stores the ResourceManager associated with this Addressables instance.
        /// </summary>
        public static ResourceManager ResourceManager
        {
            get { return m_Addressables.ResourceManager; }
        }

        internal static AddressablesImpl Instance
        {
            get { return m_Addressables; }
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void RegisterPlayModeStateChange()
        {
            EditorApplication.playModeStateChanged += SetAddressablesReInitFlagOnExitPlayMode;
        }

        static void SetAddressablesReInitFlagOnExitPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode || change == PlayModeStateChange.ExitingPlayMode)
                reinitializeAddressables = true;
        }

#endif

        /// <summary>
        /// The path to the Addressables Library subfolder
        /// </summary>
        public const string LibraryPath = "Library/com.unity.addressables/";

        /// <summary>
        /// The path to the Addressables Build Reports subfolder
        /// </summary>
        public const string BuildReportPath = "Library/com.unity.addressables/BuildReports/";

        /// <summary>
        /// Loads a single Addressable asset identified by an <see cref="IResourceLocation"/>.
        /// </summary>
        /// <typeparam name="TObject">The type of the asset.</typeparam>
        /// <param name="key">The key of the location of the asset.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is the loaded asset of the type `TObject`.</returns>
        public static AsyncOperationHandle<TObject> LoadAssetAsync<TObject>(string key)
        {
            return m_Addressables.LoadAssetAsync<TObject>(key);
        }

        /// <summary>
        /// Release asset.
        /// </summary>
        /// <typeparam name="TObject">The type of the object being released</typeparam>
        /// <param name="obj">The asset to release.</param>
        public static void Release<TObject>(TObject obj)
        {
            m_Addressables.Release(obj);
        }

        /// <summary>
        /// Release the operation and its associated resources.
        /// </summary>
        /// <typeparam name="TObject">The type of the AsyncOperationHandle being released</typeparam>
        /// <param name="handle">The operation handle to release.</param>
        public static void Release<TObject>(AsyncOperationHandle<TObject> handle)
        {
            m_Addressables.Release(handle);
        }

        /// <summary>
        /// Release the operation and its associated resources.
        /// </summary>
        /// <param name="handle">The operation handle to release.</param>
        public static void Release(AsyncOperationHandle handle)
        {
            m_Addressables.Release(handle);
        }

        /// <summary>
        /// Releases and destroys an object that was created via Addressables.InstantiateAsync.
        /// </summary>
        /// <param name="instance">The GameObject instance to be released and destroyed.</param>
        /// <returns>Returns true if the instance was successfully released.</returns>
        public static bool ReleaseInstance(GameObject instance)
        {
            return m_Addressables.ReleaseInstance(instance);
        }

        /// <summary>
        /// Releases and destroys an object that was created via Addressables.InstantiateAsync.
        /// </summary>
        /// <param name="handle">The handle to the game object to destroy, that was returned by InstantiateAsync.</param>
        /// <returns>Returns true if the instance was successfully released.</returns>
        public static bool ReleaseInstance(AsyncOperationHandle handle)
        {
            m_Addressables.Release(handle);
            return true;
        }

        /// <summary>
        /// Releases and destroys an object that was created via Addressables.InstantiateAsync.
        /// </summary>
        /// <param name="handle">The handle to the game object to destroy, that was returned by InstantiateAsync.</param>
        /// <returns>Returns true if the instance was successfully released.</returns>
        public static bool ReleaseInstance(AsyncOperationHandle<GameObject> handle)
        {
            m_Addressables.Release(handle);
            return true;
        }

        /// <summary>
        /// Instantiate a single object. Note that the dependency loading is done asynchronously, but generally the actual instantiate is synchronous.
        /// </summary>
        /// <param name="location">The location of the Object to instantiate.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        /// <param name="trackHandle">If true, Addressables will track this request to allow it to be released via the result object.</param>
        /// <returns>The operation handle for the request.</returns>
        /// <seealso cref="Addressables.InstantiateAsync(IResourceLocation, Transform, bool, bool)"/>
        //[Obsolete("We have added Async to the name of all asynchronous methods (UnityUpgradable) -> InstantiateAsync(*)", true)]
        [Obsolete]
        public static AsyncOperationHandle<GameObject> Instantiate(IResourceLocation location, Transform parent = null, bool instantiateInWorldSpace = false, bool trackHandle = true)
        {
            return InstantiateAsync(location, new InstantiationParameters(parent, instantiateInWorldSpace), trackHandle);
        }

        /// <summary>
        /// Instantiate a single object.
        /// </summary>
        /// <remarks>
        /// Loads a Prefab and instantiates a copy of the prefab into the active scene or parent GameObject. The Prefab and any resources associated with it
        /// are loaded asynchronously, whereas the instantiation is executed synchronously. In the situation where the Prefab and resources are already loaded,
        /// the entire operation is completed synchronously.
        /// 
        /// Most versions of the function shares the same parameters(position, rotation, etc.) as [Object.Instantiate](xref:UnityEngine.Object.Instantiate*).
        /// You can create an [InstantiationParameters](xref:UnityEngine.ResourceManagement.ResourceProviders.InstantiationParameters) struct to store these
        /// parameters, pass it into the function instead.
        /// </remarks>
        /// <param name="location">The location of the Object to instantiate.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        /// <param name="trackHandle">If true, Addressables will track this request to allow it to be released via the result object.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is a GameObject.</returns>
        /// <example>
        /// The example below instantiates a GameObject using by a `key`, specifically an <see cref="AssetReference"/>. By default `trackHandle` is set to `true`.
        /// Since the instance is tracked, a reference from the instance to the handle is stored and released via <see cref="Addressables.ReleaseInstance(GameObject)"/>.
        /// Alternatively a reference to the operation handle can be stored and released via <see cref="Release(AsyncOperationHandle)"/>, similar to the second example below.
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingInstantiateAsync.cs" region="SAMPLE_OBJECT_TRACKED"/>
        /// </example>
        /// <example>
        /// The example below shows how to release a GameObject when `trackHandle` is set to `false`. The instance is not tracked and cannot be used to
        /// release the object, instead a reference to the operation handle is stored and released via <see cref="Release(AsyncOperationHandle)"/>.
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingInstantiateAsync.cs" region="SAMPLE_OBJECT_UNTRACKED"/>
        /// </example>
        /// <example>
        /// The example below instantiates a GameObject using an <see cref="IResourceLocation"/>.
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingInstantiateAsync.cs" region="SAMPLE_LOCATION"/>
        /// </example>
        public static AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, Transform parent = null, bool instantiateInWorldSpace = false, bool trackHandle = true)
        {
            return m_Addressables.InstantiateAsync(location, new InstantiationParameters(parent, instantiateInWorldSpace), trackHandle);
        }

        /// <summary>
        /// Instantiate a single object.
        /// </summary>
        /// <param name="location">The location of the Object to instantiate.</param>
        /// <param name="position">The position of the instantiated object.</param>
        /// <param name="rotation">The rotation of the instantiated object.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        /// <param name="trackHandle">If true, Addressables will track this request to allow it to be released via the result object.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is a GameObject.</returns>
        public static AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, Vector3 position, Quaternion rotation, Transform parent = null, bool trackHandle = true)
        {
            return m_Addressables.InstantiateAsync(location, position, rotation, parent, trackHandle);
        }

        /// <summary>
        /// Instantiate a single object.
        /// </summary>
        /// <param name="key">The key of the location of the Object to instantiate.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        /// <param name="trackHandle">If true, Addressables will track this request to allow it to be released via the result object.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is a GameObject.</returns>
        public static AsyncOperationHandle<GameObject> InstantiateAsync(string key, Transform parent = null, bool instantiateInWorldSpace = false, bool trackHandle = true)
        {
            return m_Addressables.InstantiateAsync(key, parent, instantiateInWorldSpace, trackHandle);
        }

        /// <summary>
        /// Instantiate a single object.
        /// </summary>
        /// <param name="key">The key of the location of the Object to instantiate.</param>
        /// <param name="position">The position of the instantiated object.</param>
        /// <param name="rotation">The rotation of the instantiated object.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        /// <param name="trackHandle">If true, Addressables will track this request to allow it to be released via the result object.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is a GameObject.</returns>
        public static AsyncOperationHandle<GameObject> InstantiateAsync(string key, Vector3 position, Quaternion rotation, Transform parent = null, bool trackHandle = true)
        {
            return m_Addressables.InstantiateAsync(key, position, rotation, parent, trackHandle);
        }

        /// <summary>
        /// Instantiate a single object.
        /// </summary>
        /// <param name="key">The key of the location of the Object to instantiate.</param>
        /// <param name="instantiateParameters">Parameters for instantiation.</param>
        /// <param name="trackHandle">If true, Addressables will track this request to allow it to be released via the result object.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is a GameObject.</returns>
        public static AsyncOperationHandle<GameObject> InstantiateAsync(string key, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            return m_Addressables.InstantiateAsync(key, instantiateParameters, trackHandle);
        }

        /// <summary>
        /// Instantiate a single object.
        /// </summary>
        /// <param name="location">The location of the Object to instantiate.</param>
        /// <param name="instantiateParameters">Parameters for instantiation.</param>
        /// <param name="trackHandle">If true, Addressables will track this request to allow it to be released via the result object.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is a GameObject.</returns>
        public static AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            return m_Addressables.InstantiateAsync(location, instantiateParameters, trackHandle);
        }

        /// <summary>
        /// Load scene.
        /// </summary>
        /// <param name="key">The key of the location of the scene to load.</param>
        /// <param name="loadMode">Scene load mode.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The operation handle for the request.</returns>
        /// <seealso cref="Addressables.LoadSceneAsync(object, LoadSceneMode, bool, int)"/>
        //[Obsolete("We have added Async to the name of all asynchronous methods (UnityUpgradable) -> LoadSceneAsync(*)", true)]
        [Obsolete]
        public static AsyncOperationHandle<SceneInstance> LoadScene(string key, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)
        {
            return LoadSceneAsync(key, loadMode, activateOnLoad, priority);
        }

        /// <summary>
        /// Load scene.
        /// </summary>
        /// <param name="location">The location of the scene to load.</param>
        /// <param name="loadMode">Scene load mode.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The operation handle for the request.</returns>
        /// <seealso cref="Addressables.LoadSceneAsync(IResourceLocation, LoadSceneMode, bool, int)"/>
        //[Obsolete("We have added Async to the name of all asynchronous methods (UnityUpgradable) -> LoadSceneAsync(*)", true)]
        [Obsolete]
        public static AsyncOperationHandle<SceneInstance> LoadScene(IResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)
        {
            return LoadSceneAsync(location, loadMode, activateOnLoad, priority);
        }

        /// <summary>
        /// Loads an Addressable Scene asset.
        /// </summary>
        /// <remarks>
        /// The <paramref name="loadMode"/>, <paramref name="activateOnLoad"/>, and <paramref name="priority"/> parameters correspond to
        /// the parameters used in the Unity [SceneManager.LoadSceneAsync](https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html)
        /// method.
        ///
        /// See [Loading Scenes](xref:addressables-api-load-asset-async#loading-scenes) for more details.
        /// </remarks>
        /// <param name="key">The key of the location of the scene to load.</param>
        /// <param name="loadMode">Scene load mode.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(string key, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)
        {
            return m_Addressables.LoadSceneAsync(key, loadMode, activateOnLoad, priority);
        }

        /// <summary>
        /// Loads an Addressable Scene asset.
        /// </summary>
        /// <param name="location">The location of the scene to load.</param>
        /// <param name="loadMode">Scene load mode.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(IResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)
        {
            return m_Addressables.LoadSceneAsync(location, loadMode, activateOnLoad, priority);
        }

        /// <summary>
        /// Release scene
        /// </summary>
        /// <remarks>
        /// UnloadSceneAsync releases a previously loaded scene. The scene must have been activated to be unloaded.
        ///
        /// Passing UnloadSceneOptions.UnloadAllEmbeddedSceneObjects will unload assets embedded in the scene. The default is UploadSceneOptions.None
        /// which will only unload the scene's GameObjects.
        /// </remarks>
        /// <param name="scene">The SceneInstance to release.</param>
        /// <param name="unloadOptions">Specify behavior for unloading embedded scene objecs</param>
        /// <param name="autoReleaseHandle">If true, the handle will be released automatically when complete.</param>
        /// <returns>The operation handle for the scene unload.</returns>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingUnloadSceneAsync.cs" region="SAMPLE"/>
        /// </example>
        public static AsyncOperationHandle<SceneInstance> UnloadSceneAsync(SceneInstance scene, UnloadSceneOptions unloadOptions, bool autoReleaseHandle = true)
        {
            return m_Addressables.UnloadSceneAsync(scene, unloadOptions, autoReleaseHandle);
        }

        /// <summary>
        /// Release scene
        /// </summary>
        /// <param name="handle">The handle returned by LoadSceneAsync for the scene to release.</param>
        /// <param name="unloadOptions">Specify behavior for unloading embedded scene objecs</param>
        /// <param name="autoReleaseHandle">If true, the handle will be released automatically when complete.</param>
        /// <returns>The operation handle for the scene unload.</returns>
        public static AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle handle, UnloadSceneOptions unloadOptions, bool autoReleaseHandle = true)
        {
            return m_Addressables.UnloadSceneAsync(handle, unloadOptions, autoReleaseHandle);
        }

        /// <summary>
        /// Release scene
        /// </summary>
        /// <param name="scene">The SceneInstance to release.</param>
        /// <param name="autoReleaseHandle">If true, the handle will be released automatically when complete.</param>
        /// <returns>The operation handle for the scene unload.</returns>
        public static AsyncOperationHandle<SceneInstance> UnloadSceneAsync(SceneInstance scene, bool autoReleaseHandle = true)
        {
            return m_Addressables.UnloadSceneAsync(scene, UnloadSceneOptions.None, autoReleaseHandle);
        }

        /// <summary>
        /// Release scene
        /// </summary>
        /// <param name="handle">The handle returned by LoadSceneAsync for the scene to release.</param>
        /// <param name="autoReleaseHandle">If true, the handle will be released automatically when complete.</param>
        /// <returns>The operation handle for the scene unload.</returns>
        public static AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle handle, bool autoReleaseHandle = true)
        {
            return m_Addressables.UnloadSceneAsync(handle, UnloadSceneOptions.None, autoReleaseHandle);
        }

        /// <summary>
        /// Release scene
        /// </summary>
        /// <param name="handle">The handle returned by LoadSceneAsync for the scene to release.</param>
        /// <param name="autoReleaseHandle">If true, the handle will be released automatically when complete.</param>
        /// <returns>The operation handle for the scene unload.</returns>
        public static AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle<SceneInstance> handle, bool autoReleaseHandle = true)
        {
            return m_Addressables.UnloadSceneAsync(handle, UnloadSceneOptions.None, autoReleaseHandle);
        }
    }
}
