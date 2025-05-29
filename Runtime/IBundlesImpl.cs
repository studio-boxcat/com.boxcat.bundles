using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Bundles
{
    internal interface IBundlesImpl
    {
        IAssetOp<TObject> Load<TObject>(Address address) where TObject : Object;
        TObject LoadSync<TObject>(Address address) where TObject : Object;
        IAssetOp<TObject> Load<TObject>(AssetLocation loc) where TObject : Object;
        TObject LoadSync<TObject>(AssetLocation loc) where TObject : Object;
        IAssetOp<Scene> LoadScene(Address address);
    }
}