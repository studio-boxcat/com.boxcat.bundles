using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;

namespace UnityEngine.ResourceManagement.AsyncOperations
{
    internal class InitalizationObjectsOperation : AsyncOperationBase<bool>
    {
        private AsyncOperationHandle<ResourceManagerRuntimeData> m_RtdOp;
        private AddressablesImpl m_Addressables;
        private AsyncOperationHandle<IList<AsyncOperationHandle>> m_DepOp;

        public void Init(AsyncOperationHandle<ResourceManagerRuntimeData> rtdOp, AddressablesImpl addressables)
        {
            m_RtdOp = rtdOp;
            m_Addressables = addressables;
            m_Addressables.ResourceManager.RegisterForCallbacks();
        }

        protected override string DebugName
        {
            get { return "InitializationObjectsOperation"; }
        }

        internal bool LogRuntimeWarnings(string pathToBuildLogs)
        {
            if (!File.Exists(pathToBuildLogs))
                return false;

            PackedPlayModeBuildLogs runtimeBuildLogs = JsonUtility.FromJson<PackedPlayModeBuildLogs>(File.ReadAllText(pathToBuildLogs));
            bool messageLogged = false;
            foreach (var log in runtimeBuildLogs.RuntimeBuildLogs)
            {
                messageLogged = true;
                switch (log.Type)
                {
                    case LogType.Warning:
                        Addressables.LogWarning(log.Message);
                        break;
                    case LogType.Error:
                        Addressables.LogError(log.Message);
                        break;
                    case LogType.Log:
                        Addressables.Log(log.Message);
                        break;
                }
            }

            return messageLogged;
        }

        /// <inheritdoc />
        protected override bool InvokeWaitForCompletion()
        {
            if (IsDone)
                return true;
            if (m_RtdOp.IsValid() && !m_RtdOp.IsDone)
                m_RtdOp.WaitForCompletion();

            m_RM?.Update(Time.unscaledDeltaTime);

            if (!HasExecuted)
                InvokeExecute();

            if (m_DepOp.IsValid() && !m_DepOp.IsDone)
                m_DepOp.WaitForCompletion();
            m_RM?.Update(Time.unscaledDeltaTime);

            return IsDone;
        }

        protected override void Execute()
        {
            var rtd = m_RtdOp.Result;
            if (rtd == null)
            {
                Addressables.LogError("RuntimeData is null.  Please ensure you have built the correct Player Content.");
                Complete(true, true, "");
                return;
            }

            string buildLogsPath = m_Addressables.ResolveInternalId(PlayerPrefs.GetString(Addressables.kAddressablesRuntimeBuildLogPath));
            if (LogRuntimeWarnings(buildLogsPath))
                File.Delete(buildLogsPath);

            Complete(true, true, "");
        }
    }
}
