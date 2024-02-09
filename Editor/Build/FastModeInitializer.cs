using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace UnityEditor.AddressableAssets.Settings
{
    static class FastModeInitializer
    {
        public static void Initialize(AddressablesImpl addressables, AddressableAssetSettings settings)
        {
            Debug.Log("[Addressables] FastModeInitializer");
            addressables.SetResourceLocator(new AddressableAssetSettingsLocator(settings));
            addressables.ResourceManager.ResourceProviders[(int) ResourceProviderType.AssetDatabase] ??= new AssetDatabaseProvider();
        }
    }
}