using System;
using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets.Util
{
    public static class ExtensionMethods
    {
        public static void SafeInvoke<T>(this Action<T> callback, T arg)
        {
            Assert.IsNotNull(callback, "Callback is null");

            try
            {
                callback.Invoke(arg);
            }
            catch (Exception e)
            {
                L.Exception(e);
            }
        }

        public static void WaitForComplete(this AsyncOperation op)
        {
#if DEBUG
            var timeout = DateTime.Now.AddSeconds(1);
            while (op.isDone is false)
            {
                if (DateTime.Now > timeout)
                    throw new TimeoutException("Operation did not complete in time: " + op);
            }
#else
            while (op.isDone is false) ;
#endif
        }
    }
}