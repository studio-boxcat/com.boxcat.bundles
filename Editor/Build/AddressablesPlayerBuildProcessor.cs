using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;

/// <summary>
/// Maintains Addresssables build data when processing a player build.
/// </summary>
public class AddressablesPlayerBuildProcessor : BuildPlayerProcessor
{
    /// <summary>
    /// Returns the player build processor callback order.
    /// </summary>
    public override int callbackOrder => 1;

    [InitializeOnLoadMethod]
    static void CleanTemporaryPlayerBuildData()
    {
        var linkPath = GetAssetsLinkPath(AddressableDefaultSettings.Settings, false);
        var guid = AssetDatabase.AssetPathToGUID(linkPath);
        if (!string.IsNullOrEmpty(guid)) AssetDatabase.DeleteAsset(linkPath);
        else if (File.Exists(linkPath)) File.Delete(linkPath);
    }

    /// <summary>
    /// Invoked before performing a Player build. Maintains building Addressables step and processing Addressables build data.
    /// </summary>
    /// <param name="buildPlayerContext"></param>
    public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
    {
        var settings = AddressableDefaultSettings.Settings;
        Assert.IsNotNull(settings, "AddressableAssetSettings object is null");
        Assert.IsNotNull(buildPlayerContext, "BuildPlayerContext object is null");

        // Add asset bundles with catalog.bin to streaming assets.
        if (Directory.Exists(PathConfig.BuildPath_BundleRoot))
        {
            buildPlayerContext.AddAdditionalPathToStreamingAssets(
                PathConfig.BuildPath_BundleRoot, PathConfig.RuntimeStreamingAssetsSubFolder);
        }

        // Copy link.xml into Assets folder.
        {
            var srcPath = PathConfig.BuildPath_LinkXML;
            Assert.IsTrue(File.Exists(srcPath), $"Link.xml file not found at {srcPath}");

            var dstPath = GetAssetsLinkPath(settings, true);
            File.Copy(srcPath, dstPath, true);
            AssetDatabase.ImportAsset(dstPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.DontDownloadFromCacheServer);
        }
    }

    static string GetAssetsLinkPath(AddressableAssetSettings settings, bool createFolder)
    {
        Assert.IsNotNull(settings, "AddressableAssetSettings object is null");

        var folderPath = settings.ConfigFolder;
        if (createFolder && !Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            AssetDatabase.ImportAsset(folderPath);
        }
        return Path.Combine(folderPath, "link.xml");
    }
}