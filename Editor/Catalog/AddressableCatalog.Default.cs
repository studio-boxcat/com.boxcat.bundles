using UnityEditor;
using UnityEngine;

namespace Bundles.Editor
{
    public partial class AddressableCatalog
    {
        private const string _key = "q8FnVbLc";

        private static AddressableCatalog _default;

        public static AddressableCatalog Default
        {
            get
            {
                return _default ??= AddressablesUtils.Load<AddressableCatalog>((AssetGUID) GetGuid());

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