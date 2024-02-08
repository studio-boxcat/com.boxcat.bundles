using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using UnityEngine.AddressableAssets.ResourceLocators;
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
    /// A container for data pertaining to a specific Resource Locator.  Used mainly to determine if a content catalog
    /// needs to be updated.
    /// </summary>
    public class ResourceLocatorInfo
    {
        /// <summary>
        /// The Resource Locator that has been loaded into the Addressables system.
        /// </summary>
        public IResourceLocator Locator { get; private set; }

        /// <summary>
        /// Contstruct a ResourceLocatorInfo for a given Resource Locator.
        /// </summary>
        /// <param name="loc">The IResourceLocator to track.</param>
        /// <param name="remoteCatalogLocation">The location for the remote catalog.  Typically this location contains exactly two dependeices,
        /// the first one pointing to the remote hash file.  The second dependency pointing to the local hash file.</param>
        public ResourceLocatorInfo(IResourceLocator loc)
        {
            Locator = loc;
        }
    }

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

        /// <summary>
        /// Construct a new InvalidKeyException.
        /// </summary>
        /// <param name="key">The key that caused the exception.</param>
        public InvalidKeyException(object key) : this(key, typeof(object))
        {
        }

        private AddressablesImpl m_Addressables;

        /// <summary>
        /// Construct a new InvalidKeyException.
        /// </summary>
        /// <param name="key">The key that caused the exception.</param>
        /// <param name="type">The type of the key that caused the exception.</param>
        public InvalidKeyException(object key, Type type)
        {
            Key = key;
            Type = type;
        }

        internal InvalidKeyException(object key, Type type, AddressablesImpl addr)
        {
            Key = key;
            Type = type;
            m_Addressables = addr;
        }

        ///<inheritdoc cref="InvalidKeyException"/>
        public InvalidKeyException()
        {
        }

        ///<inheritdoc/>
        public InvalidKeyException(string message) : base(message)
        {
        }

        ///<inheritdoc/>
        public InvalidKeyException(string message, Exception innerException) : base(message, innerException)
        {
        }

        ///<inheritdoc/>
        protected InvalidKeyException(SerializationInfo message, StreamingContext context) : base(message, context)
        {
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

            HashSet<Type> typesAvailableForKey = GetTypesForKey(keyString);
            if (typesAvailableForKey.Count == 0)
                return $"{base.Message} No Location found for Key={keyString}";

            if (typesAvailableForKey.Count == 1)
            {
                Type availableType = null;
                foreach (Type type in typesAvailableForKey)
                    availableType = type;
                if (availableType == null)
                    return string.Format(BaseInvalidKeyMessageFormat, base.Message, keyString, Type);
                return $"{base.Message} No Asset found with for Key={keyString}. Key exists as Type={availableType}, which is not assignable from the requested Type={Type}";
            }

            StringBuilder csv = new StringBuilder(512);
            int count = 0;
            foreach (Type type in typesAvailableForKey)
            {
                count++;
                csv.Append(count > 1 ? $", {type}" : type.ToString());
            }

            return $"{base.Message} No Asset found with for Key={keyString}. Key exists as multiple Types={csv}, which is not assignable from the requested Type={Type}";
        }

        HashSet<Type> GetTypesForKey(string keyString)
        {
            HashSet<Type> typesAvailableForKey = new HashSet<Type>();
            foreach (var locator in m_Addressables.ResourceLocators)
            {
                if (!locator.Locate(keyString, null, out var locations))
                    continue;

                foreach (IResourceLocation location in locations)
                    typesAvailableForKey.Add(location.ResourceType);
            }

            return typesAvailableForKey;
        }

        bool GetTypeToKeys(string key, Dictionary<Type, List<string>> typeToKeys)
        {
            HashSet<Type> types = GetTypesForKey(key);
            if (types.Count == 0)
                return false;

            foreach (Type type in types)
            {
                if (!typeToKeys.TryGetValue(type, out List<string> keysForType))
                    typeToKeys.Add(type, new List<string>() {key});
                else
                    keysForType.Add(key);
            }

            return true;
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
        /// Used to resolve a string using addressables config values
        /// </summary>
        /// <param name="id">The internal id to resolve.</param>
        /// <returns>Returns the string that the internal id represents.</returns>
        public static string ResolveInternalId(string id)
        {
            return m_Addressables.ResolveInternalId(id);
        }

        const string k_AddressablesLogConditional = "ADDRESSABLES_LOG_ALL";

        /// <summary>
        /// The path to the Addressables Library subfolder
        /// </summary>
        public const string LibraryPath = "Library/com.unity.addressables/";

        /// <summary>
        /// The path to the Addressables Build Reports subfolder
        /// </summary>
        public const string BuildReportPath = "Library/com.unity.addressables/BuildReports/";

        [Conditional(k_AddressablesLogConditional)]
        internal static void InternalSafeSerializationLog(string msg, LogType logType = LogType.Log)
        {
            if (m_AddressablesInstance == null)
                return;
            switch (logType)
            {
                case LogType.Warning:
                    m_AddressablesInstance.LogWarning(msg);
                    break;
                case LogType.Error:
                    m_AddressablesInstance.LogError(msg);
                    break;
                case LogType.Log:
                    m_AddressablesInstance.Log(msg);
                    break;
            }
        }

        [Conditional(k_AddressablesLogConditional)]
        internal static void InternalSafeSerializationLogFormat(string format, LogType logType = LogType.Log, params object[] args)
        {
            if (m_AddressablesInstance == null)
                return;
            switch (logType)
            {
                case LogType.Warning:
                    m_AddressablesInstance.LogWarningFormat(format, args);
                    break;
                case LogType.Error:
                    m_AddressablesInstance.LogErrorFormat(format, args);
                    break;
                case LogType.Log:
                    m_AddressablesInstance.LogFormat(format, args);
                    break;
            }
        }

        /// <summary>
        /// Log can be used to write a Debug level log message.
        /// </summary>
        /// <remarks>
        /// Log works the same as <see cref="Debug.Log(object)">Debug.Log</see>. Addressables only logs warnings and errors so by default this function does not log anything.
        /// </remarks>
        /// <param name="msg">The msg to log</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLog.cs" region="SAMPLE" />
        /// </example>
        /// <seealso href="xref:addressables-asset-settings#enable-all-logging">Enable all logging</seealso>
        [Conditional(k_AddressablesLogConditional)]
        public static void Log(string msg)
        {
            m_Addressables.Log(msg);
        }

        /// <summary>
        /// LogFormat can be used to write a formatted log message.
        /// </summary>
        /// <remarks>
        /// LogFormat supports Composite Formatting and works the same way as [Debug.LogFormat](xref:UnityEngine.Debug.LogFormat(System.String,System.Object[])). Addressables logs warnings and errors so by default this function will **not** log.
        /// </remarks>
        /// <seealso href="xref:addressables-asset-settings#enable-all-logging">Enable all logging</seealso>
        /// <param name="format">The string with format tags.</param>
        /// <param name="args">The args used to fill in the format tags.</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLogFormat.cs" region="SAMPLE" />
        /// </example>
        [Conditional(k_AddressablesLogConditional)]
        public static void LogFormat(string format, params object[] args)
        {
            m_Addressables.LogFormat(format, args);
        }

        /// <summary>
        /// LogWarning can be used to write a log message.
        /// </summary>
        /// <remarks>
        /// LogWarning works the same way as [Debug.LogWarning](xref:UnityEngine.Debug.LogWarning(System.Object)). Addressables logs warnings and errors so by default this function will log.
        /// </remarks>
        /// <param name="msg">The msg to log</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLogWarning.cs" region="SAMPLE" />
        /// </example>
        public static void LogWarning(string msg)
        {
            m_Addressables.LogWarning(msg);
        }

        /// <summary>
        /// LogFormat can be used to write a formatted log message.
        /// </summary>
        /// <remarks>
        /// LogWarningFormat supports Composite Formatting and works the same way as [Debug.LogWarningFormat](xref:UnityEngine.Debug.LogWarningFormat(System.String,System.Object[])). Addressables logs warnings and errors so by default this function will log.
        /// </remarks>
        /// <param name="format">The string with format tags.</param>
        /// <param name="args">The args used to fill in the format tags.</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLogWarningFormat.cs" region="SAMPLE" />
        /// </example>
        public static void LogWarningFormat(string format, params object[] args)
        {
            m_Addressables.LogWarningFormat(format, args);
        }

        /// <summary>
        /// Write an error level log message.
        /// </summary>
        /// <remarks>
        /// LogError can be used to write an Error message. LogError works the same way as <see cref="Debug.LogError(object)" />. Addressables logs warnings and errors so by default this function will log.
        /// </remarks>
        /// <param name="msg">The msg to log</param>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLogError.cs" region="SAMPLE"/>
        /// <seealso href="xref:addressables-asset-settings#enable-all-logging">Enable all logging</seealso>
        public static void LogError(string msg)
        {
            m_Addressables.LogError(msg);
        }

        /// <summary>
        /// Write an exception as a log message. 
        /// </summary>
        /// <remarks>
        /// LogException can be used to convert an exception to a log message. The exception is stringified. If the operation is in a failed state, the exception is logged at an Error logging level. If not the exception is logged at a Debug logging level.
        /// Addressables logs warnings and errors so if the operation is not in a failed state by default this function will not log.
        /// </remarks>
        /// <param name="op">The operation handle.</param>
        /// <param name="ex">The exception.</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLogException.cs" region="SAMPLE_ASYNC_OP"/>
        /// </example>
        /// <seealso href="xref:addressables-asset-settings#enable-all-logging">Enable all logging</seealso>
        public static void LogException(AsyncOperationHandle op, Exception ex)
        {
            m_Addressables.LogException(op, ex);
        }

        /// <summary>
        /// Write an exception as a debug log message.
        /// </summary>
        /// <remarks>
        /// LogException can be used to convert an exception to a log message. The exception is stringified and logged at a Debug logging level. Addressables logs warnings and errors so by default this function will not log.
        /// </remarks>
        /// <param name="ex">The exception.</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLogException.cs" region="SAMPLE"/>
        /// </example>
        public static void LogException(Exception ex)
        {
            m_Addressables.LogException(ex);
        }

        /// <summary>
        /// Write an error level formatted log message.
        /// </summary>
        /// <remarks>
        /// LogErrorFormat can be used to write a formatted Error level log message. LogErrorFormat supports Composite Formatting and works the same way as <see cref="Debug.LogErrorFormat(string, object[])" />. Addressables logs warnings and errors so by default this function will log.
        /// </remarks>
        /// <param name="format">The string with format tags.</param>
        /// <param name="args">The args used to fill in the format tags.</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLogErrorFormat.cs" region="SAMPLE"/>
        /// </example>
        public static void LogErrorFormat(string format, params object[] args)
        {
            m_Addressables.LogErrorFormat(format, args);
        }

        /// <summary>
        /// Loads a single Addressable asset identified by an <see cref="IResourceLocation"/>.
        /// </summary>
        /// <typeparam name="TObject">The type of the asset.</typeparam>
        /// <param name="key">The key of the location of the asset.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is the loaded asset of the type `TObject`.</returns>
        public static AsyncOperationHandle<TObject> LoadAssetAsync<TObject>(object key)
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
        public static AsyncOperationHandle<GameObject> InstantiateAsync(object key, Transform parent = null, bool instantiateInWorldSpace = false, bool trackHandle = true)
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
        public static AsyncOperationHandle<GameObject> InstantiateAsync(object key, Vector3 position, Quaternion rotation, Transform parent = null, bool trackHandle = true)
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
        public static AsyncOperationHandle<GameObject> InstantiateAsync(object key, InstantiationParameters instantiateParameters, bool trackHandle = true)
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
        public static AsyncOperationHandle<SceneInstance> LoadScene(object key, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)
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
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(object key, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)
        {
            return m_Addressables.LoadSceneAsync(key, new LoadSceneParameters(loadMode), activateOnLoad, priority);
        }

        /// <summary>
        /// Loads an Addressable Scene asset.
        /// </summary>
        /// <param name="key">The key of the location of the scene to load.</param>
        /// <param name="loadSceneParameters">Scene load mode.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(object key, LoadSceneParameters loadSceneParameters, bool activateOnLoad = true, int priority = 100)
        {
            return m_Addressables.LoadSceneAsync(key, loadSceneParameters, activateOnLoad, priority);
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
            return m_Addressables.LoadSceneAsync(location, new LoadSceneParameters(loadMode), activateOnLoad, priority);
        }

        /// <summary>
        /// Loads an Addressable Scene asset.
        /// </summary>
        /// <param name="location">The location of the scene to load.</param>
        /// <param name="loadSceneParameters">Scene load parameters.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(IResourceLocation location, LoadSceneParameters loadSceneParameters, bool activateOnLoad = true, int priority = 100)
        {
            return m_Addressables.LoadSceneAsync(location, loadSceneParameters, activateOnLoad, priority);
        }

        /// <summary>
        /// Release scene
        /// </summary>
        /// <param name="scene">The SceneInstance to release.</param>
        /// <param name="autoReleaseHandle">If true, the handle will be released automatically when complete.</param>
        /// <returns>The operation handle for the request.</returns>
        /// <seealso cref="UnloadSceneAsync(SceneInstance, bool)"/>
        /// <seealso href="xref:synchronous-addressables">Synchronous Addressables</seealso>
        //[Obsolete("We have added Async to the name of all asynchronous methods (UnityUpgradable) -> UnloadSceneAsync(*)", true)]
        [Obsolete]
        public static AsyncOperationHandle<SceneInstance> UnloadScene(SceneInstance scene, bool autoReleaseHandle = true)
        {
            return UnloadSceneAsync(scene, autoReleaseHandle);
        }

        /// <summary>
        /// Release scene
        /// </summary>
        /// <param name="handle">The handle returned by LoadSceneAsync for the scene to release.</param>
        /// <param name="autoReleaseHandle">If true, the handle will be released automatically when complete.</param>
        /// <returns>The operation handle for the request.</returns>
        /// <seealso cref="UnloadSceneAsync(AsyncOperationHandle, bool)"/>
        /// <seealso href="xref:synchronous-addressables">Synchronous Addressables</seealso>
        //[Obsolete("We have added Async to the name of all asynchronous methods (UnityUpgradable) -> UnloadSceneAsync(*)", true)]
        [Obsolete]
        public static AsyncOperationHandle<SceneInstance> UnloadScene(AsyncOperationHandle handle, bool autoReleaseHandle = true)
        {
            return UnloadSceneAsync(handle, autoReleaseHandle);
        }

        /// <summary>
        /// Release scene
        /// </summary>
        /// <param name="handle">The handle returned by LoadSceneAsync for the scene to release.</param>
        /// <param name="autoReleaseHandle">If true, the handle will be released automatically when complete.</param>
        /// <returns>The operation handle for the request.</returns>
        /// <seealso cref="UnloadSceneAsync(AsyncOperationHandle{SceneInstance}, bool)"/>
        /// <seealso href="xref:synchronous-addressables">Synchronous Addressables</seealso>
        //[Obsolete("We have added Async to the name of all asynchronous methods (UnityUpgradable) -> UnloadSceneAsync(*)", true)]
        [Obsolete]
        public static AsyncOperationHandle<SceneInstance> UnloadScene(AsyncOperationHandle<SceneInstance> handle, bool autoReleaseHandle = true)
        {
            return UnloadSceneAsync(handle, autoReleaseHandle);
        }

        /// <summary>
        /// Release scene
        /// </summary>
        /// <param name="handle">The handle returned by LoadSceneAsync for the scene to release.</param>
        /// <param name="unloadOptions">Specify behavior for unloading embedded scene objecs</param>
        /// <param name="autoReleaseHandle">If true, the handle will be released automatically when complete.</param>
        /// <returns>The operation handle for the request.</returns>
        /// <seealso cref="UnloadSceneAsync(SceneInstance, UnloadSceneOptions, bool)"/>
        /// <seealso href="xref:synchronous-addressables">Synchronous Addressables</seealso>
        //[Obsolete("We have added Async to the name of all asycn methods (UnityUpgradable) -> UnloadSceneAsync(*)", true)]
        [Obsolete]
        public static AsyncOperationHandle<SceneInstance> UnloadScene(AsyncOperationHandle<SceneInstance> handle, UnloadSceneOptions unloadOptions, bool autoReleaseHandle = true)
        {
            return UnloadSceneAsync(handle, unloadOptions, autoReleaseHandle);
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
