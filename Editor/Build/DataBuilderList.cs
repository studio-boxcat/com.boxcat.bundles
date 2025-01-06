using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build
{
    public static class DataBuilderList
    {
        public static IDataBuilder Builder => _builder_PackedMode;

        private const string _prefKey_AssetDatabase = "AddressableAssets:DataBuilderList:AssetDatabase";

        private static IDataBuilder _editorCache;
        public static IDataBuilder Editor
        {
            get
            {
                if (_editorCache is not null) return _editorCache;
                var simulate = PlayerPrefs.GetInt(_prefKey_AssetDatabase, 1) is 1;
                return _editorCache = simulate ? _builder_FastMode : _builder_PackedPlayMode;
            }
        }

        private static readonly IDataBuilder _builder_PackedMode = new BuildScriptPackedMode();
        private static readonly IDataBuilder _builder_FastMode = new BuildScriptFastMode();
        private static readonly IDataBuilder _builder_PackedPlayMode = new BuildScriptPackedPlayMode();


        public static void UseAssetDatabaseForEditor(bool value)
        {
            PlayerPrefs.SetInt(_prefKey_AssetDatabase, value ? 1 : 0);
            _editorCache = value ? _builder_FastMode : _builder_PackedPlayMode;
        }

        public static void Clear()
        {
            _builder_FastMode.ClearCachedData();
            _builder_PackedPlayMode.ClearCachedData();
            _builder_PackedMode.ClearCachedData();
        }
    }
}