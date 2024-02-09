using System;
using System.Diagnostics;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UnityEngine.AddressableAssets
{
    public static class L
    {
        [Conditional("DEBUG")]
        public static void I(string msg)
        {
            Debug.Log(msg);
        }

        [Conditional("DEBUG")]
        public static void I(string format, params object[] args)
        {
            Debug.LogFormat(format, args);
        }

        [Conditional("DEBUG")]
        public static void W(string msg)
        {
            Debug.LogWarning(msg);
        }

        [Conditional("DEBUG")]
        public static void W(string format, params object[] args)
        {
            Debug.LogWarningFormat(format, args);
        }

        public static void E(string msg)
        {
            Debug.LogError(msg);
        }

        public static void E(string format, params object[] args)
        {
            Debug.LogErrorFormat(format, args);
        }

        public static void Exception(AsyncOperationHandle op, Exception ex)
        {
            Debug.LogException(ex);
            if (op.Status == AsyncOperationStatus.Failed)
                E($"Failed op : {op.DebugName}");
        }

        public static void Exception(Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}