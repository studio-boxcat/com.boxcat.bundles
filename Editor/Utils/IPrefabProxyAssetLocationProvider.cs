#nullable enable

using Bundles.Editor;
using UnityEngine;

namespace Bundles
{
    public interface IPrefabProxyAssetLocationProvider : IPrefabProxyAssetProvider
    {
        new AssetLocation? Get(PrefabProxy proxy);

        GameObject? IPrefabProxyAssetProvider.Get(PrefabProxy proxy)
        {
            var loc = Get(proxy);
            return loc.TryGetValue(out var locVal)
                ? EditorBundles.LoadAsset<GameObject>(locVal)
                : null;
        }
    }
}