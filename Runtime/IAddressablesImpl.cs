using UnityEngine.AddressableAssets.AsyncOperations;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets
{
    interface IAddressablesImpl
    {
        IAssetOp<TObject> LoadAssetAsync<TObject>(string address) where TObject : Object;
        TObject LoadAsset<TObject>(string address) where TObject : Object;
        IAssetOp<Scene> LoadSceneAsync(string address);
    }
}