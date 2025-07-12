using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;

namespace Bundles
{
    internal static class ExtensionMethods
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
                L.E(e);
            }
        }

        public static void SafeInvoke<TObject>(this Action<IAssetOp<TObject>, TObject, object, int> onComplete,
            IAssetOp<TObject> op, TObject result, object payloadObj, int payloadInt)
        {
            Assert.IsNotNull(onComplete, "Callback is null");

            try
            {
                onComplete.Invoke(op, result, payloadObj, payloadInt);
            }
            catch (Exception e)
            {
                L.E(e);
            }
        }

        public static void WaitForComplete(this UnityWebRequestAsyncOperation op)
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

        public static AssetBundle WaitForComplete(this AssetBundleCreateRequest req)
        {
            Assert.IsFalse(req.isDone, "Operation is already done");
            return req.assetBundle; // accessing asset before isDone is true will stall the loading process.
        }
    }
}