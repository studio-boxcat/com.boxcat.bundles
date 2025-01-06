namespace UnityEngine.AddressableAssets.ResourceProviders
{
    internal interface IResourceProvider
    {
        AsyncOperation LoadAsync<T>(AssetBundle bundle, Address address);
        object GetResult(AsyncOperation op);
    }
}