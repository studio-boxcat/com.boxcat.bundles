namespace UnityEngine.AddressableAssets
{
    internal static class PathConfig
    {
        public const string AA = "aa";

        private static string _loadPath
        {
            get
            {
#if UNITY_EDITOR
                const string platform =
#if UNITY_ANDROID
                    "Android";
#elif UNITY_IOS
                    "iOS";
#else
                    NOT_SUPPORTED
#endif
                return Application.dataPath + "/../" + LibraryPath + platform;
#else
                return Application.streamingAssetsPath + "/" + AA;
#endif
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
                    _loadPath + "/catalog.bin";
            }
        }

        private static char[] _bundlePathFormat;

        public static string GetAssetBundleLoadPath(AssetBundleId bundleId)
        {
            _bundlePathFormat ??= (_loadPath + "/XXXX").ToCharArray();
            bundleId.WriteHex4(_bundlePathFormat, _bundlePathFormat.Length - 4);
            return new string(_bundlePathFormat);
        }

#if UNITY_EDITOR
        public const string LibraryPath = "Library/com.boxcat.bundles/";
        public const string BuildReportPath = "Library/com.boxcat.bundles/BuildReports/";
        public const string TempPath = "Library/com.boxcat.bundles/Temp/";

        private static string _buildPath;
        public static string GetBuildPath(UnityEditor.BuildTarget buildTarget) => $"{LibraryPath}{buildTarget}";
#endif
    }
}