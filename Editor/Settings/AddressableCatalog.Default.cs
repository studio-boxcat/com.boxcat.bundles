using UnityEngine;
using UnityEngine.AddressableAssets.Util;

namespace UnityEditor.AddressableAssets
{
    public partial class AddressableCatalog
    {
        private const string _key = "JvdB2Lb8";

        private static AddressableCatalog _default;

        public static AddressableCatalog Default
        {
            get
            {
                var guid = GetGuid();
                var path = AssetDatabase.GUIDToAssetPath(guid);
                return AssetDatabase.LoadAssetAtPath<AddressableCatalog>(path);

                static string GetGuid()
                {
                    var guid = PlayerPrefs.GetString(_key, "");
                    if (guid.Length is not 0) return guid;

                    var guids = AssetDatabase.FindAssets("t:" + nameof(AddressableCatalog));
                    if (guids.Length is 0) throw new System.Exception("AddressableAssetSettings object not found. Please create one.");
                    if (guids.Length > 1) L.E("Multiple AddressableAssetSettings objects found. Using the first one.");

                    guid = guids[0];
                    PlayerPrefs.SetString(_key, guid);
                    return guid;
                }
            }
        }
    }
}