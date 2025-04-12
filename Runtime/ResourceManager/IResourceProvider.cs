namespace UnityEngine.AddressableAssets
{
    internal interface IResourceProvider
    {
        AsyncOperation LoadAsync<T>(AssetBundle bundle, string assetName);
        object GetResult(AsyncOperation op);
    }
}