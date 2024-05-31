using System;
using System.IO;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets
{
    public static class AddressableBuilder
    {
        /// <summary>
        /// Runs the active player data build script to create runtime data.
        /// See the [BuildPlayerContent](xref:addressables-api-build-player-content) documentation for more details.
        /// </summary>
        public static DataBuildResult BuildPlayerContent(AddressableAssetSettings settings, IDataBuilder builder, BuildTarget target)
        {
            Assert.IsNotNull(settings, "AddressableAssetSettings must not be null");
            Assert.IsNotNull(builder, "IDataBuilder must not be null");

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

            var result = builder.BuildData(settings, target);
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

        public static DataBuildResult BuildPlayerContent()
        {
            return BuildPlayerContent(AddressableDefaultSettings.Settings, DataBuilderList.Builder, EditorUserBuildSettings.activeBuildTarget);
        }

        /// <summary>
        /// Deletes all created runtime data for the active player data builder.
        /// </summary>
        public static void CleanPlayerContent()
        {
            var settings = AddressableDefaultSettings.Settings;
            if (settings == null)
            {
                if (EditorApplication.isUpdating)
                    Debug.LogError("Addressable Asset Settings does not exist.  EditorApplication.isUpdating was true.");
                else if (EditorApplication.isCompiling)
                    Debug.LogError("Addressable Asset Settings does not exist.  EditorApplication.isCompiling was true.");
                else
                    Debug.LogError("Addressable Asset Settings does not exist.  Failed to create.");
                return;
            }

            DataBuilderList.Clear();
            AssetDatabase.Refresh();
        }
    }
}