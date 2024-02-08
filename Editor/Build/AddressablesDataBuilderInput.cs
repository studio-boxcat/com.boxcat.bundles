using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.AddressableAssets.Build
{
    /// <summary>
    /// Data builder context object for Addressables.
    /// </summary>
    public class AddressablesDataBuilderInput
    {
        /// <summary>
        /// The main addressables settings object.
        /// </summary>
        public AddressableAssetSettings AddressableSettings { get; private set; }

        /// <summary>
        /// Build target group.
        /// </summary>
        public BuildTargetGroup TargetGroup { get; private set; }

        /// <summary>
        /// Build target.
        /// </summary>
        public BuildTarget Target { get; private set; }

        /// <summary>
        /// Registry of files created during the build
        /// </summary>
        public FileRegistry Registry { get; private set; }

        //used only by tests to inject custom info into build...
        internal string PathSuffix = string.Empty;

        /// <summary>
        /// The name of the default Runtime Catalog file.
        /// </summary>
        public string RuntimeCatalogFilename = "catalog.bin";

        /// <summary>
        /// Creates a default context object with values taken from the AddressableAssetSettings parameter.
        /// </summary>
        /// <param name="settings">The settings object to pull values from.</param>
        public AddressablesDataBuilderInput(AddressableAssetSettings settings)
        {
            SetAllValues(settings,
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                EditorUserBuildSettings.activeBuildTarget);
        }

        internal void SetAllValues(AddressableAssetSettings settings, BuildTargetGroup buildTargetGroup, BuildTarget buildTarget)
        {
            AddressableSettings = settings;

            TargetGroup = buildTargetGroup;
            Target = buildTarget;
            Registry = new FileRegistry();
        }

        internal bool IsBuildAndRelease = false;
        internal bool IsContentUpdateBuild = false;

        internal IBuildLogger Logger { get; set; }
    }
}
