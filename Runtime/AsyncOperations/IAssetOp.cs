#nullable enable
using System;
using System.Collections;

namespace Bundles
{
    public interface IAssetOp<TResult>
    {
        bool TryGetResult(out TResult result);
        TResult WaitForCompletion();

        // default implementation: box the onComplete callback
        void AddOnComplete(Action<TResult> onComplete) => AddOnComplete(
            static (_, result, po, _) => ((Action<TResult>) po!)(result), payloadObj: onComplete, payloadInt: 0);
        void AddOnComplete(Action<TResult, object?> onComplete, object? payload) => AddOnComplete(
            (_, result, po, _) => onComplete(result, po), payloadObj: payload, payloadInt: 0);
        void AddOnComplete(Action<TResult, int> onComplete, int payload) => AddOnComplete(
            static (_, result, po, pi) => ((Action<TResult, int>) po!)(result, pi), payloadObj: onComplete, payloadInt: payload);
        void AddOnComplete(Action<TResult, object?, int> onComplete, object? payloadObj, int payloadInt) => AddOnComplete(
            (_, result, po, pi) => onComplete(result, po, pi), payloadObj: payloadObj, payloadInt: payloadInt);
        void AddOnComplete(Action<IAssetOp<TResult>, TResult, object?> onComplete, object? payload) => AddOnComplete(
            (op, result, po, _) => onComplete(op, result, po), payloadObj: payload, 0);
        void AddOnComplete(Action<IAssetOp<TResult>, TResult, int> onComplete, int payload) => AddOnComplete(
            static (op, result, po, pi) => ((Action<IAssetOp<TResult>, TResult, int>) po!)(op, result, pi), payloadObj: onComplete, payloadInt: payload);
        void AddOnComplete(Action<IAssetOp<TResult>, TResult, object?, int> onComplete, object? payloadObj, int payloadInt);
    }

    public static class AssetOpUtils
    {
        public static bool IsDone<TResult>(this IAssetOp<TResult> op) =>
            op.TryGetResult(out _);

        public static IEnumerator ToCoroutine<TResult>(this IAssetOp<TResult> op)
        {
            while (op.TryGetResult(out _) is false)
                yield return null;
        }
    }
}