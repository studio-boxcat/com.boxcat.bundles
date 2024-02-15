using UnityEngine.AddressableAssets.AsyncOperations;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets
{
    interface IAddressablesImpl
    {
        IAssetOp<TObject> LoadAssetAsync<TObject>(string address) where TObject : Object;
        IAssetOp<Scene> LoadSceneAsync(string address);
    }

    class AddressablesImpl : IAddressablesImpl
    {
        readonly ResourceManager _resourceManager;


        public AddressablesImpl(ResourceCatalog catalog)
        {
            _resourceManager = new ResourceManager(catalog);
        }

        public IAssetOp<TObject> LoadAssetAsync<TObject>(string address) where TObject : Object
        {
            return _resourceManager.LoadAsset<TObject>(address);
        }

        public IAssetOp<Scene> LoadSceneAsync(string address)
        {
            return _resourceManager.LoadScene(address);
        }
    }
}