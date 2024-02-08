namespace UnityEngine.ResourceManagement
{
    public static class ResourcePath
    {
        public const string StreamingAssetsSubFolder = "aa";

        public static string BuildPath
        {
            get
            {
                return "Library/com.unity.addressables/" + StreamingAssetsSubFolder + "/"
                       + PlatformMappingService.GetPlatformPathSubFolder();
            }
        }

        public static string BuildPath_LogsJson => BuildPath + "/buildLogs.json";
        public static string BuildPath_LinkXML => BuildPath + "/AddressablesLink/link.xml";

        public static string RuntimePath
        {
            get
            {
#if UNITY_EDITOR
                return Application.dataPath + "/../" + BuildPath;
#else
                return Application.streamingAssetsPath + "/" + StreamingAssetsSubFolder;
#endif
            }
        }

        public static string RuntimePath_CatalogBin => RuntimePath + "/catalog.bin";

        public static string GetAssetBundleLoadPath(string fileName)
        {
            return RuntimePath + "/" + fileName;
        }
    }
}