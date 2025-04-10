using System;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets
{
    public static class AddressableBuilder
    {
        /// <summary>
        /// Runs the active player data build script to create runtime data.
        /// See the [BuildPlayerContent](xref:addressables-api-build-player-content) documentation for more details.
        /// </summary>
        public static DataBuildResult BuildPlayerContent(AddressableCatalog catalog, BuildTarget target)
        {
            Assert.IsNotNull(catalog, "AddressableAssetSettings must not be null");

            var result = BuildScriptPackedMode.BuildData(catalog, target);
            if (!string.IsNullOrEmpty(result.Error))
            {
                L.E(result.Error);
                L.E($"[AddressableBuilder] Addressable content build failure (duration : {TimeSpan.FromSeconds(result.Duration):g})");
            }
            else
                L.I($"[AddressableBuilder] Addressable content successfully built (duration : {TimeSpan.FromSeconds(result.Duration):g})");

            AssetDatabase.Refresh();
            return result;
        }
    }
}