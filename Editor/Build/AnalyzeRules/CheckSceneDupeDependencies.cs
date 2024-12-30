using System.Collections.Generic;
using System.Linq;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    /// <summary>
    /// Rule class to check scene dependencies for duplicates
    /// </summary>
    class CheckSceneDupeDependencies : BundleRuleBase
    {
        /// <inheritdoc />
        public override string ruleName => "Check Scene to Addressable Duplicate Dependencies";

        /// <summary>
        /// Clear analysis and calculate built in resources and corresponding bundle dependencies for scenes
        /// </summary>
        /// <param name="catalog">The current Addressables catalog object</param>
        /// <returns>List of results from analysis</returns>
        public override List<AnalyzeResult> RefreshAnalysis(AddressableCatalog catalog)
        {
            ClearAnalysis();

            string[] scenePaths = (from editorScene in EditorBuildSettings.scenes
                where editorScene.enabled
                select editorScene.path).ToArray();
            return CalculateBuiltInResourceDependenciesToBundleDependecies(catalog, scenePaths);
        }

        /// <inheritdoc />
        internal protected override string[] GetResourcePaths()
        {
            List<string> scenes = new List<string>(EditorBuildSettings.scenes.Length);
            foreach (EditorBuildSettingsScene settingsScene in EditorBuildSettings.scenes)
            {
                if (settingsScene.enabled)
                    scenes.Add(settingsScene.path);
            }

            return scenes.ToArray();
        }
    }
}
