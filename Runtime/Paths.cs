using System.Text;
using UnityEngine;

namespace Bundles
{
    internal static class Paths
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

        private static StringBuilder _bundlePathFormat;

        public static string GetAssetBundleLoadPath(AssetBundleId bundleId)
        {
            if (_bundlePathFormat is null)
            {
                _bundlePathFormat = new StringBuilder(_loadPath, _loadPath.Length + "/XXXX".Length);
                _bundlePathFormat.Append('/', repeatCount: "/XXXX".Length);
            }

            _bundlePathFormat.Hex4(bundleId.Val(), startIndex: _loadPath.Length + 1 /* after last '/' */);
            return _bundlePathFormat.ToString();
        }

#if UNITY_EDITOR
        public const string LibraryPath = "Library/com.boxcat.bundles/";
        public const string BuildReportPath = LibraryPath + "BuildReports/";
        public const string TempPath = LibraryPath + "Temp/";

        private static string _buildPath;
        public static string GetBuildPath(UnityEditor.BuildTarget buildTarget) => $"{LibraryPath}{buildTarget}";
#endif
    }
}