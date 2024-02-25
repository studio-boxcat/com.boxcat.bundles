using UnityEditor.AddressableAssets.Settings;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Only saves the guid of the settings asset to PlayerPrefs.  All catalog data is generated directly from the settings as needed.
    /// </summary>
    public class BuildScriptFastMode : BuildScriptBase
    {
        /// <inheritdoc />
        protected override DataBuildResult BuildDataImplementation(AddressableAssetSettings settings, BuildTarget target)
        {
            EditorAddressablesImplFactory.UseAssetDatabase(settings);
            return new DataBuildResult();
        }
    }
}