using System;
using System.Collections;

namespace UnityEngine.AddressableAssets
{
    public interface IAssetOp<TResult>
    {
        bool TryGetResult(out TResult result);
        TResult WaitForCompletion();
        void AddOnComplete(Action<TResult> onComplete);
        void AddOnComplete(Action<TResult, object> onComplete, object payload);
        void AddOnComplete(Action<IAssetOp<TResult>, TResult, object> onComplete, object payload);
    }

    public static class AssetOpUtils
    {
        public static IEnumerator ToCoroutine<TResult>(this IAssetOp<TResult> op)
        {
            while (op.TryGetResult(out _) is false)
                yield return null;
        }
    }
}