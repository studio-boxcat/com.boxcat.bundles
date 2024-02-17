namespace UnityEngine.AddressableAssets.ResourceProviders
{
    public interface IResourceProvider
    {
        AsyncOperation LoadAsync(AssetBundle bundle, Address address);
        object GetResult(AsyncOperation op);
    }
}