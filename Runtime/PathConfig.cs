namespace UnityEngine.AddressableAssets
{
    public static class PathConfig
    {
        public const string StreamingAssetsSubFolder = "aa";

        private static string _runtimePath
        {
            get
            {
#if UNITY_EDITOR
                return Application.dataPath + "/../" + BuildPath;
#endif

                return Application.streamingAssetsPath + "/" + StreamingAssetsSubFolder;
            }
        }

        public static string CatalogUri
        {
            get
            {
                return
#if UNITY_EDITOR || !UNITY_ANDROID
                    "file://" +
#endif
                    _runtimePath + "/catalog.bin";
            }
        }

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
        public const string TempPath = "Library/com.boxcat.addressables/Temp/";

        private static string _buildPath;
        public static string BuildPath => _buildPath ??= $"{LibraryPath}{UnityEditor.EditorUserBuildSettings.activeBuildTarget}";
#endif
    }
}