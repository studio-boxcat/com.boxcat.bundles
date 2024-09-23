#if UNITY_2022_2_OR_NEWER
using UnityEngine.UIElements;

namespace UnityEditor.AddressableAssets.BuildReportVisualizer
{
    interface IAddressableView
    {
        void CreateGUI(VisualElement rootVisualElement);
    }
}
#endif
