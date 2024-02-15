namespace UnityEngine.AddressableAssets.ResourceProviders
{
    public interface IResourceProvider
    {
        AsyncOperation Execute(AssetBundle bundle, Address address);
        object GetResult(AsyncOperation op);
    }
}