using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.AddressableAssets.Build.BuildPipelineTasks
{
    /// <summary>
    /// The BuildTask used to extract write data from the build.
    /// </summary>
    public class ExtractDataTask : IBuildTask
    {
        /// <summary>
        /// The ExtractDataTask version.
        /// </summary>
        public int Version => 1;

        /// <summary>
        /// Get the injected write data of the task.
        /// </summary>
        public IBundleWriteData WriteData => m_WriteData;

        /// <summary>
        /// The build context of the task.
        /// </summary>
        public IBuildContext BuildContext => m_BuildContext;

#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        private IBundleWriteData m_WriteData;

        [InjectContext(ContextUsage.In)]
        internal IBuildContext m_BuildContext;
#pragma warning restore 649

        /// <summary>
        /// Runs the ExtractDataTask.  The data for this task is all injected context so no operations are performed in the Run step.
        /// </summary>
        /// <returns>Success.</returns>
        public ReturnCode Run() => ReturnCode.Success;
    }
}
