using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Util;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Uses data built by BuildScriptPacked class.  This script just sets up the correct variables and runs.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildScriptPackedPlayMode.asset", menuName = "Addressables/Content Builders/Use Existing Build (requires built groups)")]
    public class BuildScriptPackedPlayMode : BuildScriptBase
    {
        /// <inheritdoc />
        public override bool CanBuildData<T>()
        {
            return typeof(T).IsAssignableFrom(typeof(AddressablesPlayModeBuildResult));
        }

        /// <inheritdoc />
        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput)
        {
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            var dataBuildTarget = builderInput.Target;

            if (BuildPipeline.GetBuildTargetGroup(dataBuildTarget) != BuildTargetGroup.Standalone)
                L.W($"Asset bundles built with build target {dataBuildTarget} may not be compatible with running in the Editor.");

            EditorAddressablesImplFactory.UseCatalog();
            IDataBuilderResult res = new AddressablesPlayModeBuildResult() {Duration = timer.Elapsed.TotalSeconds};
            return (TResult) res;
        }
    }
}