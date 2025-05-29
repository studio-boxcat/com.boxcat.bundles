using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.Assertions;

namespace Bundles
{
    internal class AssetOpBlock
    {
        public string AssetName;
        public AssetBundleIndex Bundle;
        public IResourceProvider Provider;

        public readonly ResourceCatalog Catalog;
        public readonly AssetBundleLoader Loader;
        public readonly CompleteCallback OnComplete;

        private readonly List<AssetOpBlock> _pool;


        public AssetOpBlock(ResourceCatalog catalog, AssetBundleLoader loader, List<AssetOpBlock> pool)
        {
            Catalog = catalog;
            Loader = loader;
            OnComplete = CompleteCallback.Create();
            _pool = pool;
        }

        public void Init(string assetName, AssetBundleIndex bundle, IResourceProvider provider)
        {
            AssetName = assetName;
            Bundle = bundle;
            Provider = provider;
        }

        public void Return()
        {
            Assert.IsTrue(OnComplete.IsEmpty, "OnComplete should be empty before disposal");
            _pool.Add(this);
        }
    }

    public partial class AssetOp<TResult> : IAssetOp<TResult>
    {
        // AssetOpBlock or TResult
        [NotNull] private object _data;
        // null or AsyncOperation
        // null: asset bundle is loading or done
        // AsyncOperation: asset is loading
        private object _op;


        internal AssetOp(AssetOpBlock data)
        {
            _data = data;

            // If the main asset bundle is already fully loaded including dependencies,
            // we can start the asset load operation immediately.
            var bundleIndex = data.Bundle;
            if (data.Loader.TryGetResolvedBundle(bundleIndex, out var bundle))
            {
                OnAllBundleLoaded(bundle);
                return;
            }

            // Possible to be resolved while StartResolve() is called.
            // In this case, callbackRegistered will be false, so we can call OnDepLoaded immediately.
            var deps = data.Catalog.GetDependencies(bundleIndex);
            var done = data.Loader.ResolveAsync(bundleIndex, deps, this, _onDepLoaded);
            if (done) OnAllBundleLoaded(data.Loader.GetResolvedBundle(bundleIndex)); // bundle is already loaded
        }

        public override string ToString()
        {
            return _data is TResult result
                ? "AssetOp:" + result + " (Loaded)"
                : "AssetOp:" + ((AssetOpBlock) _data).AssetName + " (Loading)";
        }

        public bool TryGetResult(out TResult result)
        {
            // If result is already loaded, return true.
            if (_data is TResult r)
            {
                Assert.IsNull(_op, "Operation should be null after completion");
                result = r;
                return true;
            }

            // If dep is not loaded, _op will be null.
            if (_op is not AsyncOperation op || op.isDone is false)
            {
                result = default;
                return false;
            }

            // operation is done but result is not yet set.
            var payload = AsyncOpPayloads.PopData(op);
            Assert.AreEqual(this, payload, $"Payload mismatch: payload={payload?.GetType()}, this={GetType()}");
            op.completed -= _onComplete; // remove payload and call OnComplete to prevent double call.

            // manually call OnComplete to set the result.
            OnComplete(op);
            Assert.IsNull(_op, "Operation should be null after completion");
            Assert.IsTrue(_data is TResult, "Result should be set after completion");
            result = (TResult) _data;
            return true;
        }

        public TResult WaitForCompletion()
        {
            // If result is already loaded, return it.
            if (_data is TResult asset)
                return asset;

            // If dep is not loaded yet, _op will be null.
            // In this case, load dependencies immediately.
            var b = (AssetOpBlock) _data;
            if (_op is null)
            {
                b.Loader.CompleteResolveImmediate(b.Bundle);
                Assert.IsNotNull(_op, "OnDepLoaded() should have been called after dependencies are loaded");
            }

            // Immediately complete the operation.
            b.Provider.GetResult((AsyncOperation) _op); // accessing asset before isDone is true will stall the loading process.
            Assert.IsNull(_op, "Operation should be null after completion");
            Assert.IsTrue(_data is TResult, "Result should be set after completion");

            return (TResult) _data;
        }

        private static readonly Action<AssetBundle, object> _onDepLoaded = static (bundle, thiz) => ((AssetOp<TResult>) thiz).OnAllBundleLoaded(bundle);

        private void OnAllBundleLoaded(AssetBundle bundle)
        {
            Assert.IsNull(_op, "Operation should be null before execution");

            var b = (AssetOpBlock) _data;
            var op = b.Provider.LoadAsync<TResult>(bundle, b.AssetName);
            _op = op;

            if (op.isDone)
            {
                OnComplete(op);
                return;
            }

            AsyncOpPayloads.SetData(op, this);
            op.completed += _onComplete;
        }

        private static readonly Action<AsyncOperation> _onComplete = static op =>
        {
            var thiz = AsyncOpPayloads.PopData(op);
            Assert.AreEqual(typeof(AssetOp<TResult>), thiz.GetType(),
                $"Payload mismatch: expected={typeof(AssetOp<TResult>)}, actual={thiz.GetType()}");
            ((AssetOp<TResult>) thiz).OnComplete(op);
        };

        private void OnComplete(AsyncOperation req)
        {
            var op = (AsyncOperation) _op;
            Assert.AreEqual(req, op, "Operation mismatch");
            Assert.IsTrue(op.isDone, "Operation is not done");

            var b = (AssetOpBlock) _data;
            var result = (TResult) b.Provider.GetResult(op);
            _data = result; // boxing could happen for Scene, but it's very rare operation.
            _op = null;

            b.OnComplete.Invoke(this, result);
            b.Return();
        }
    }
}