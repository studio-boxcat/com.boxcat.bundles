using System;
using System.IO;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine.AddressableAssets;
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

            if (Directory.Exists(PathConfig.BuildPath))
            {
                try
                {
                    Directory.Delete(PathConfig.BuildPath, true);
                }
                catch (Exception e)
                {
                    L.E(e);
                }
            }

            var builder = new BuildScriptPackedMode();
            var result = builder.BuildData(catalog, target);
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