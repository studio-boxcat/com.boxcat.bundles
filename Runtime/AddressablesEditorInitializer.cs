#if UNITY_EDITOR
using System;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UnityEngine.AddressableAssets
{
    public static class AddressablesEditorInitializer
    {
        public static Func<object, AsyncOperationHandle<IResourceLocator>> CreatePlayModeInitializationOperation;
    }
}
#endif