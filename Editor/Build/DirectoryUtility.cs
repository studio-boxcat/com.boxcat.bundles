using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class DirectoryUtility
{
    internal static void DeleteDirectory(string directoryPath, bool onlyIfEmpty = true, bool recursiveDelete = true)
    {
        if (!Directory.Exists(directoryPath))
            return;

        var isEmpty = !Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories).Any()
                      && !Directory.EnumerateDirectories(directoryPath, "*", SearchOption.AllDirectories).Any();
        if (!onlyIfEmpty || isEmpty)
        {
            // check if the folder is valid in the AssetDatabase before deleting through standard file system
            string relativePath = directoryPath.Replace("\\", "/").Replace(Application.dataPath, "Assets");
            if (AssetDatabase.IsValidFolder(relativePath))
                AssetDatabase.DeleteAsset(relativePath);
            else
                Directory.Delete(directoryPath, recursiveDelete);
        }
    }
}