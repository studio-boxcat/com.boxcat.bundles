#if UNITY_EDITOR
using System;

namespace UnityEngine.AddressableAssets
{
    static class AddressablesEditorInitializer
    {
        internal static Action<AddressablesImpl> InitializeOverride;
    }
}
#endif