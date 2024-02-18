namespace UnityEditor.AddressableAssets.Settings
{
    internal class AddressablesAssetPostProcessor : AssetPostprocessor
    {
        static readonly AddressableAssetUtility.SortedDelegate<string[], string[], string[], string[]> s_OnPostProcessHandler = new();

        public static AddressableAssetUtility.SortedDelegate<string[], string[], string[], string[]> OnPostProcess => s_OnPostProcessHandler;

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            s_OnPostProcessHandler.Invoke(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
        }
    }
}