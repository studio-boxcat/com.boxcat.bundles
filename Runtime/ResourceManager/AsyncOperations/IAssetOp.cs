using System;

namespace UnityEngine.AddressableAssets.AsyncOperations
{
    public interface IAssetOp<TResult>
    {
        bool TryGetResult(out TResult result);
        TResult WaitForCompletion();
        void AddOnComplete(Action<TResult> onComplete);
        void AddOnComplete(Action<TResult, object> onComplete, object payload);
        void AddOnComplete(Action<IAssetOp<TResult>, TResult, object> onComplete, object payload);
    }
}