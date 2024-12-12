using System.IO;
using UnityEditor.AddressableAssets;
using UnityEditor.Build;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Util;
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

    /// <summary>
    /// Invoked before performing a Player build. Maintains building Addressables step and processing Addressables build data.
    /// </summary>
    /// <param name="buildPlayerContext"></param>
    public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
    {
        var catalog = AddressableCatalog.Default;
        Assert.IsNotNull(catalog, "AddressableAssetSettings object is null");
        Assert.IsNotNull(buildPlayerContext, "BuildPlayerContext object is null");

        // Add asset bundles with catalog.bin to streaming assets.
        if (Directory.Exists(PathConfig.BuildPath_BundleRoot))
        {
            buildPlayerContext.AddAdditionalPathToStreamingAssets(
                PathConfig.BuildPath_BundleRoot, PathConfig.RuntimeStreamingAssetsSubFolder);
        }
        else
        {
            L.W("[AddressablesPlayerBuildProcessor] No Addressables build data found. Skipping streaming assets copy.");
        }
    }
}