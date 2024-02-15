using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Only saves the guid of the settings asset to PlayerPrefs.  All catalog data is generated directly from the settings as needed.
    /// </summary>
    [CreateAssetMenu(fileName = nameof(BuildScriptFastMode) + ".asset", menuName = "Addressables/Content Builders/Use Asset Database (fastest)")]
    public class BuildScriptFastMode : BuildScriptBase
    {
        /// <inheritdoc />
        public override bool CanBuildData<T>() => typeof(T).IsAssignableFrom(typeof(AddressablesPlayModeBuildResult));

        /// <inheritdoc />
        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput)
        {
            Assert.IsNotNull(builderInput.AddressableSettings, "Invalid AddressableSettings object");

            EditorAddressablesImplFactory.UseAssetDatabase(builderInput.AddressableSettings);
            IDataBuilderResult res = new AddressablesPlayModeBuildResult() {OutputPath = "", Duration = 0};
            return (TResult) res;
        }
    }
}