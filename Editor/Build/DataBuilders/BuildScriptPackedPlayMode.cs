using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;

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

            var buildLogs = new PackedPlayModeBuildLogs();
            var dataBuildTarget = builderInput.Target;

            if (BuildPipeline.GetBuildTargetGroup(dataBuildTarget) != BuildTargetGroup.Standalone)
            {
                buildLogs.RuntimeBuildLogs.Add(new PackedPlayModeBuildLogs.RuntimeBuildLog(LogType.Warning,
                    $"Asset bundles built with build target {dataBuildTarget} may not be compatible with running in the Editor."));
            }

            if (buildLogs.RuntimeBuildLogs.Count > 0)
            {
                var buildLogsPath = PathConfig.BuildPath_LogsJson;
                File.WriteAllText(buildLogsPath, JsonUtility.ToJson(buildLogs));
            }

            EditorAddressablesImplFactory.UseCatalog();
            IDataBuilderResult res = new AddressablesPlayModeBuildResult() {Duration = timer.Elapsed.TotalSeconds};
            return (TResult) res;
        }
    }
}