using System.Diagnostics;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Uses data built by BuildScriptPacked class.  This script just sets up the correct variables and runs.
    /// </summary>
    public class BuildScriptPackedPlayMode : BuildScriptBase
    {
        protected override DataBuildResult BuildDataImplementation(AddressableAssetSettings settings, BuildTarget target)
        {
            var timer = new Stopwatch();
            timer.Start();
            EditorAddressablesImplFactory.UseCatalog();
            return new DataBuildResult { Duration = timer.Elapsed.TotalSeconds };
        }
    }
}