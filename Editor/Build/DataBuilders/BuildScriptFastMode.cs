using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Only saves the guid of the catalog asset to PlayerPrefs.  All catalog data is generated directly from the catalog as needed.
    /// </summary>
    public class BuildScriptFastMode : BuildScriptBase
    {
        /// <inheritdoc />
        protected override DataBuildResult BuildDataImplementation(AddressableCatalog catalog, BuildTarget target)
        {
            EditorAddressablesImplFactory.UseAssetDatabase(catalog);
            return new DataBuildResult();
        }
    }
}