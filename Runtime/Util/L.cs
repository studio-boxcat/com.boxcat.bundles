using System;
using System.Diagnostics;

namespace UnityEngine.AddressableAssets.Util
{
    static class L
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
        public static void W(bool condition, string msg)
        {
            if (condition)
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

        public static void E(Object context, string msg)
        {
            Debug.LogError(msg, context);
        }

        public static void E(Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}