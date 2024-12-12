using System.Collections.Generic;
using System.IO;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    /// <summary>
    /// Rule class to check resource dependencies for duplicates
    /// </summary>
    class CheckResourcesDupeDependencies : BundleRuleBase
    {
        /// <inheritdoc />
        public override string ruleName => "Check Resources to Addressable Duplicate Dependencies";

        /// <summary>
        /// Clear analysis and calculate built in resources and corresponding bundle dependencies
        /// </summary>
        /// <param name="catalog">The current Addressables catalog object</param>
        /// <returns>List of results from analysis</returns>
        public override List<AnalyzeResult> RefreshAnalysis(AddressableCatalog catalog)
        {
            ClearAnalysis();
            return CalculateBuiltInResourceDependenciesToBundleDependecies(catalog, GetResourcePaths());
        }

        /// <inheritdoc />
        internal protected override string[] GetResourcePaths()
        {
            var resourceDirectory = Directory.GetDirectories("Assets", "Resources", SearchOption.AllDirectories);
            var resourcePaths = new List<string>();
            foreach (var directory in resourceDirectory)
                resourcePaths.AddRange(Directory.GetFiles(directory, "*", SearchOption.AllDirectories));
            return resourcePaths.ToArray();
        }
    }


    [InitializeOnLoad]
    class RegisterCheckResourcesDupeDependencies
    {
        static RegisterCheckResourcesDupeDependencies()
        {
            AnalyzeSystem.RegisterNewRule<CheckResourcesDupeDependencies>();
        }
    }
}
