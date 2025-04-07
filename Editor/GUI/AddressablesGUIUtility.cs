using UnityEngine;


namespace UnityEditor.AddressableAssets.GUI
{
    internal class AddressablesGUIUtility
    {
        internal static Color HeaderNormalColor
        {
            get
            {
                float shade = EditorGUIUtility.isProSkin ? 62f / 255f : 205f / 255f;
                return new Color(shade, shade, shade, 1);
            }
        }
    }
}
