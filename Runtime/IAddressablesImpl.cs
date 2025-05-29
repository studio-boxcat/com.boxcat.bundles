using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Bundles
{
    internal interface IAddressablesImpl
    {
        IAssetOp<TObject> LoadAssetAsync<TObject>(Address address) where TObject : Object;
        TObject LoadAsset<TObject>(Address address) where TObject : Object;
        IAssetOp<TObject> LoadAssetAsync<TObject>(AssetLocation loc) where TObject : Object;
        TObject LoadAsset<TObject>(AssetLocation loc) where TObject : Object;
        IAssetOp<Scene> LoadSceneAsync(Address address);
    }
}