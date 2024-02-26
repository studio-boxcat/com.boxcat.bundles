using System;

namespace UnityEngine.AddressableAssets.AsyncOperations
{
    public class CompletedOp<TResult> : IAssetOp<TResult>
    {
        readonly TResult _result;

        public CompletedOp(TResult result)
        {
            _result = result;
        }

        public bool TryGetResult(out TResult result)
        {
            result = _result;
            return true;
        }

        public TResult WaitForCompletion() => _result;

        public void AddOnComplete(Action<TResult> onComplete) => onComplete(_result);
        public void AddOnComplete(Action<TResult, object> onComplete, object payload) => onComplete(_result, payload);
        public void AddOnComplete(Action<IAssetOp<TResult>, TResult, object> onComplete, object payload) => onComplete(this, _result, payload);
    }
}