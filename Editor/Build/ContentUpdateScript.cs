using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;

#if ENABLE_CCD
using Unity.Services.Ccd.Management;
#endif

namespace UnityEditor.AddressableAssets.Build
{
#if ENABLE_CCD
    /// <summary>
    /// This is used to determine the behavior of Update a Previous Build when taking advantage of the Build & Release feature.
    /// </summary>
    public enum BuildAndReleaseContentStateBehavior
    {
        /// <summary>
        /// Uses the Previous Content State bin file path set in the AddressableAssetSettings
        /// </summary>
        UsePresetLocation = 0,
        /// <summary>
        /// Pulls the Previous Content State bin from the associated Cloud Content Delivery bucket set in the profile variables.
        /// </summary>
        UseCCDBucket = 1

    }
#endif

    /// <summary>
    /// The given state of an Asset.  Represented by its guid and hash.
    /// </summary>
    [Serializable]
    public struct AssetState : IEquatable<AssetState>
    {
        /// <summary>
        /// Asset states GUID.
        /// </summary>
        public GUID guid;

        /// <summary>
        /// Asset State hash.
        /// </summary>
        public Hash128 hash;

        /// <summary>
        /// Check if one asset state is equal to another.
        /// </summary>
        /// <param name="other">Right hand side of comparision.</param>
        /// <returns>Returns true if the Asset States are equal to one another.</returns>
        public bool Equals(AssetState other)
        {
            return guid == other.guid && hash == other.hash;
        }
    }

    /// <summary>
    /// The Cached Asset State of an Addressable Asset.
    /// </summary>
    [Serializable]
    public class CachedAssetState : IEquatable<CachedAssetState>
    {
        /// <summary>
        /// The Asset State.
        /// </summary>
        public AssetState asset;

        /// <summary>
        /// The Asset State of all dependencies.
        /// </summary>
        public AssetState[] dependencies;

        /// <summary>
        /// Checks if one cached asset state is equal to another given the asset state and dependency state.
        /// </summary>
        /// <param name="other">Right hand side of comparision.</param>
        /// <returns>Returns true if the cached asset states are equal to one another.</returns>
        public bool Equals(CachedAssetState other)
        {
            bool result = other != null && asset.Equals(other.asset);
            result &= dependencies != null && other.dependencies != null;
            result &= dependencies.Length == other.dependencies.Length;
            var index = 0;
            while (result && index < dependencies.Length)
            {
                result &= dependencies[index].Equals(other.dependencies[index]);
                index++;
            }

            return result;
        }
    }

    /// <summary>
    /// Contains methods used for the content update workflow.
    /// </summary>
    public static class ContentUpdateScript
    {
        private static string m_BinFileCachePath = "Library/com.unity.addressables/AddressablesBinFileDownload/addressables_content_state.bin";

        /// <summary>
        /// If the previous content state file location is a remote location, this path is where the file is downloaded to as part of a
        /// content update build.  In the event of a fresh build where the previous state file build path is remote, this is the location the
        /// file is built to.
        /// </summary>
        public static string PreviousContentStateFileCachePath => m_BinFileCachePath;

        internal static string GetContentStateDataPath(bool browse, AddressableAssetSettings settings)
        {
            if (settings == null)
                settings = AddressableAssetSettingsDefaultObject.Settings;
            var profileSettings = settings == null ? null : settings.profileSettings;
            string assetPath = profileSettings != null ? profileSettings.EvaluateString(settings.activeProfileId, settings.ContentStateBuildPath) : "";

            if (string.IsNullOrEmpty(assetPath))
            {
                assetPath = settings != null
                    ? settings.GetContentStateBuildPath()
                    : Path.Combine(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder, PlatformMappingService.GetPlatformPathSubFolder());
            }

            if (browse)
            {
                if (string.IsNullOrEmpty(assetPath))
                    assetPath = Application.dataPath;

                assetPath = EditorUtility.OpenFilePanel("Build Data File", Path.GetDirectoryName(assetPath), "bin");

                if (string.IsNullOrEmpty(assetPath))
                    return null;

                return assetPath;
            }

            if (!ResourceManagerConfig.ShouldPathUseWebRequest(assetPath))
            {
                try
                {
                    Directory.CreateDirectory(assetPath);
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message + "\nCheck \"Content State Build Path\" in Addressables settings. Falling back to config folder location.");
                    assetPath = Path.Combine(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                        PlatformMappingService.GetPlatformPathSubFolder());
                    Directory.CreateDirectory(assetPath);
                }
            }

#if ENABLE_CCD
            switch(settings.BuildAndReleaseBinFileOption)
            {
                case BuildAndReleaseContentStateBehavior.UsePresetLocation:
                    //do nothing
                    break;
                case BuildAndReleaseContentStateBehavior.UseCCDBucket:
                    assetPath = settings.RemoteCatalogLoadPath.GetValue(settings);
                    break;
            }
#endif

            var path = Path.Combine(assetPath, "addressables_content_state.bin");
            return path;
        }

        internal static bool GroupFilter(AddressableAssetGroup g)
        {
            if (g == null)
                return false;
            if (!g.HasSchema<BundledAssetGroupSchema>() || !g.GetSchema<BundledAssetGroupSchema>().IncludeInBuild)
                return false;
            return true;
        }
    }
}
