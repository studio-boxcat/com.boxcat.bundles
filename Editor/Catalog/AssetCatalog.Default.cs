using UnityEditor;
using UnityEngine;

namespace Bundles.Editor
{
    public partial class AssetCatalog
    {
        private const string _key = "q8FnVbLc";

        private static AssetCatalog _default;

        public static AssetCatalog Default
        {
            get
            {
                return _default ??= (AssetCatalog) AssetDatabase.LoadMainAssetAtGUID(new GUID(GetGuid()));

                static string GetGuid()
                {
                    var guid = PlayerPrefs.GetString(_key, "");
                    if (guid.Length is not 0) return guid;

                    L.W("AssetCatalog not found in PlayerPrefs. Searching for AssetCatalog object...");
                    var guids = AssetDatabase.FindAssets("t:" + nameof(AssetCatalog));
                    if (guids.Length is 0) throw new System.Exception("AssetCatalog object not found. Please create one.");
                    if (guids.Length > 1) L.E("Multiple AssetCatalog objects found. Using the first one.");

                    guid = guids[0];
                    PlayerPrefs.SetString(_key, guid);
                    return guid;
                }
            }
        }
    }
}