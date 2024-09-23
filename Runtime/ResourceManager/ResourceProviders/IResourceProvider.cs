namespace UnityEngine.AddressableAssets.ResourceProviders
{
    interface IResourceProvider
    {
        AsyncOperation LoadAsync<T>(AssetBundle bundle, Address address);
        object GetResult(AsyncOperation op);
    }
}