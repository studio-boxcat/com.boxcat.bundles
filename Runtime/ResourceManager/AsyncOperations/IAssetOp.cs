using System;

namespace UnityEngine.AddressableAssets.AsyncOperations
{
    public interface IAssetOp<TResult>
    {
        bool IsDone { get; }
        TResult Result { get; }
        void AddOnComplete(Action<TResult> onComplete);
        void AddOnComplete(Action<TResult, object> onComplete, object payload);
        TResult WaitForCompletion() => Result;

#if DEBUG
        string GetDebugName();
#endif
    }
}