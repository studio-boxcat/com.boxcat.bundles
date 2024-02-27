namespace UnityEngine.AddressableAssets.ResourceProviders
{
    public interface IResourceProvider
    {
        AsyncOperation LoadAsync<T>(AssetBundle bundle, Address address);
        object GetResult(AsyncOperation op);
    }
}