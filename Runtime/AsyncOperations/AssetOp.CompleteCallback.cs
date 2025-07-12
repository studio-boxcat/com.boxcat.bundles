using System;
using UnityEngine.Assertions;

namespace Bundles
{
    public partial class AssetOp<TResult>
    {
        public void AddOnComplete(Action<TResult> onComplete)
        {
            if (_data is TResult result)
            {
                Assert.IsNull(_op, "Operation should be null when result is set");
                onComplete(result);
                return;
            }

            ((AssetOpBlock) _data).OnComplete.Add(onComplete, false);
        }

        public void AddOnComplete(Action<TResult, object> onComplete, object payload)
        {
            if (_data is TResult result)
            {
                Assert.IsNull(_op, "Operation should be null when result is set");
                onComplete(result, payload);
                return;
            }

            ((AssetOpBlock) _data).OnComplete.Add(onComplete, false, payload);
        }

        public void AddOnComplete(Action<TResult, int> onComplete, int payload)
        {
            if (_data is TResult result)
            {
                Assert.IsNull(_op, "Operation should be null when result is set");
                onComplete(result, payload);
                return;
            }

            ((AssetOpBlock) _data).OnComplete.Add(onComplete, false, payload);
        }

        public void AddOnComplete(Action<TResult, object, int> onComplete, object payloadObj, int payloadInt)
        {
            if (_data is TResult result)
            {
                Assert.IsNull(_op, "Operation should be null when result is set");
                onComplete(result, payloadObj, payloadInt);
                return;
            }

            ((AssetOpBlock) _data).OnComplete.Add(onComplete, false, payloadObj, payloadInt);
        }

        public void AddOnComplete(Action<IAssetOp<TResult>, TResult, object> onComplete, object payload)
        {
            if (_data is TResult result)
            {
                Assert.IsNull(_op, "Operation should be null when result is set");
                onComplete(this, result, payload);
                return;
            }

            ((AssetOpBlock) _data).OnComplete.Add(onComplete, true, payload);
        }

        public void AddOnComplete(Action<IAssetOp<TResult>, TResult, int> onComplete, int payload)
        {
            if (_data is TResult result)
            {
                Assert.IsNull(_op, "Operation should be null when result is set");
                onComplete(this, result, payload);
                return;
            }

            ((AssetOpBlock) _data).OnComplete.Add(onComplete, true, payload);
        }

        public void AddOnComplete(Action<IAssetOp<TResult>, TResult, object, int> onComplete, object payloadObj, int payloadInt)
        {
            if (_data is TResult result)
            {
                Assert.IsNull(_op, "Operation should be null when result is set");
                onComplete(this, result, payloadObj, payloadInt);
                return;
            }

            ((AssetOpBlock) _data).OnComplete.Add(onComplete, true, payloadObj, payloadInt);
        }
    }
}