using System.Text;

namespace UnityEngine.AddressableAssets
{
    public static class PathConfig
    {
        public const string RuntimeStreamingAssetsSubFolder = "aa";

        static string _runtimePath
        {
            get
            {
#if UNITY_EDITOR
                return Application.dataPath + "/../" + BuildPath_BundleRoot;
#endif

                return Application.streamingAssetsPath + "/" + RuntimeStreamingAssetsSubFolder;
            }
        }

        public static string RuntimePath_CatalogBin => _runtimePath + "/catalog.bin";

        static StringBuilder _bundlePathBuilder;

        public static string GetAssetBundleLoadPath(AssetBundleId bundleId)
        {
            _bundlePathBuilder ??= new StringBuilder(_runtimePath).Append("/XX");
            bundleId.WriteHex2(_bundlePathBuilder, _bundlePathBuilder.Length - 2);
            return _bundlePathBuilder.ToString();
        }

#if UNITY_EDITOR
        public const string LibraryPath = "Library/com.unity.addressables/";
        public const string BuildReportPath = "Library/com.unity.addressables/BuildReports/";
        public const string TempPath_BundleRoot = "Temp/com.unity.addressables/AssetBundles";

        static string _buildPath;
        public static string BuildPath => _buildPath ??=
            $"{LibraryPath}{UnityEditor.EditorUserBuildSettings.activeBuildTarget}";

        public static string BuildPath_BundleRoot => BuildPath + "/AssetBundles";
        public static string BuildPath_CatalogBin => BuildPath + "/AssetBundles/catalog.bin";
        public static string BuildPath_LinkXML => BuildPath + "/link.xml";
#endif
    }
}