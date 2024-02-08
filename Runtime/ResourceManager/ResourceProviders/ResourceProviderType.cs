namespace UnityEngine.ResourceManagement.ResourceProviders
{
    public enum ResourceProviderType : byte
    {
        AssetBundle = 0,
        BundledAsset = 1,
#if UNITY_EDITOR
        AssetDatabase = 2,
#endif
    }
}