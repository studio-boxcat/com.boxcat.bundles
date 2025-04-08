namespace UnityEngine.AddressableAssets
{
    public static class PathConfig
    {
        public const string RuntimeStreamingAssetsSubFolder = "aa";

        private static string _runtimePath
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

        private static char[] _bundlePathFormat;

        public static string GetAssetBundleLoadPath(AssetBundleId bundleId)
        {
            _bundlePathFormat ??= (_runtimePath + "/XXXX").ToCharArray();
            bundleId.WriteHex4(_bundlePathFormat, _bundlePathFormat.Length - 4);
            return new string(_bundlePathFormat);
        }

#if UNITY_EDITOR
        public const string LibraryPath = "Library/com.boxcat.addressables/";
        public const string BuildReportPath = "Library/com.boxcat.addressables/BuildReports/";
        public const string TempPath_BundleRoot = "Temp/com.boxcat.addressables/AssetBundles";

        private static string _buildPath;
        public static string BuildPath => _buildPath ??=
            $"{LibraryPath}{UnityEditor.EditorUserBuildSettings.activeBuildTarget}";

        public static string BuildPath_BundleRoot => BuildPath + "/AssetBundles";
        public static string BuildPath_CatalogBin => BuildPath + "/AssetBundles/catalog.bin";
#endif
    }
}