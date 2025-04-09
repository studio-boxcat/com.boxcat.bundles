using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.AddressableAssets.Util;

namespace UnityEngine.AddressableAssets.AsyncOperations
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
            OnComplete = new CompleteCallback(new List<(object, object)>());
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

    public class AssetOp<TResult> : IAssetOp<TResult>
    {
        private AssetOpBlock _b;
        private AsyncOperation _op;
        private TResult _result;


        internal AssetOp(AssetOpBlock b)
        {
            _b = b;

            // If the main asset bundle is already fully loaded, we can start the asset load operation immediately.
            var bundleIndex = _b.Bundle;
            if (_b.Loader.TryGetResolvedBundle(bundleIndex, out var bundle))
            {
                OnDepLoaded(bundle);
                return;
            }

            // Possible to be resolved while StartResolve() is called.
            // In this case, callbackRegistered will be false, so we can call OnDepLoaded immediately.
            var deps = _b.Catalog.GetDependencies(bundleIndex);
            var indexToId = _b.Catalog.IndexToId;
            var callbackRegistered = _b.Loader.ResolveAsync(bundleIndex, deps, this, _onDepLoaded);
            if (callbackRegistered is false)
                OnDepLoaded(_b.Loader.GetResolvedBundle(bundleIndex));
        }

        public override string ToString()
        {
            if (_b is null) return "AssetOp:" + _result + " (Loaded)";
            return "AssetOp:" + _b.AssetName + " (Loading)";
        }

        public bool TryGetResult(out TResult result)
        {
            // If result is already loaded, return true.
            if (_b is null)
            {
                Assert.IsNull(_op, "Operation should be null when result is set");
                Assert.AreNotEqual(default, _result, "Result should not be null when result is set");
                result = _result;
                return true;
            }

            // If dep is not loaded, _op will be null.
            if (_op is null || _op.isDone is false)
            {
                Assert.AreEqual(default, _result, "Result should be null when operation is not done");
                result = default;
                return false;
            }

            // If OnComplete is not yet called but operation is done, call it.
            CompleteManually();
            Assert.IsNull(_op, "Operation should be null after completion");
            Assert.IsNull(_b, "Block should be null after completion");
            Assert.AreNotEqual(default, _result, "Result should not be null after completion");
            result = _result;
            return true;
        }

        public TResult WaitForCompletion()
        {
            // If result is already loaded, return it.
            if (_b is null)
                return _result;

            // If dep is not loaded yet, _op will be null.
            // In this case, load dependencies immediately.
            if (_op is null)
            {
                _b.Loader.CompleteResolveImmediate(_b.Bundle);
                Assert.IsNotNull(_op, "OnDepLoaded() should have been called after dependencies are loaded");
            }

            // Immediately complete the operation.
            _b.Provider.GetResult(_op); // accessing asset before isDone is true will stall the loading process.
            Assert.IsNull(_op, "Operation should be null after completion");
            Assert.IsNull(_b, "Block should be null after completion");
            Assert.AreNotEqual(default, "Result should not be null after completion");

            return _result;
        }

        private static readonly Action<AssetBundle, object> _onDepLoaded = static (bundle, thiz) => ((AssetOp<TResult>) thiz).OnDepLoaded(bundle);

        private void OnDepLoaded(AssetBundle bundle)
        {
            Assert.IsNull(_op, "Operation should be null before execution");
            Assert.IsNotNull(_b, "Block should not be null before execution");

            _op = _b.Provider.LoadAsync<TResult>(bundle, _b.AssetName);

            if (_op.isDone)
            {
                OnComplete(_op);
                return;
            }

            AsyncOpPayloads.SetData(_op, this);
            _op.completed += _onComplete;
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
            Assert.IsNotNull(_op, "OnComplete is called twice");
            Assert.AreEqual(req, _op, "Operation mismatch");
            Assert.IsTrue(_op.isDone, "Operation is not done");

            var b = _b;
            _b = null;
            var op = _op;
            _op = null;

            _result = (TResult) b.Provider.GetResult(op);
            b.OnComplete.Invoke(this, _result);
            b.Return();
        }

        private void CompleteManually()
        {
            Assert.IsNotNull(_op, "Operation should not be null");
            Assert.IsFalse(_op.isDone, "Operation should not be done");

            // Remove payload and call OnComplete to prevent double call.
            var payload = AsyncOpPayloads.PopData(_op);
            Assert.AreEqual(this, payload, $"Payload mismatch: payload={payload?.GetType()}, this={GetType()}");
            _op.completed -= _onComplete;

            OnComplete(_op);
        }

        public void AddOnComplete(Action<TResult> onComplete)
        {
            if (_b is null)
            {
                Assert.IsNull(_op, "Operation should be null when result is set");
                onComplete(_result);
                return;
            }

            _b.OnComplete.Add(onComplete, null);
        }

        public void AddOnComplete(Action<TResult, object> onComplete, object payload)
        {
            if (_b is null)
            {
                Assert.IsNull(_op, "Operation should be null when result is set");
                onComplete(_result, payload);
                return;
            }

            _b.OnComplete.Add(onComplete, payload);
        }

        public void AddOnComplete(Action<IAssetOp<TResult>, TResult, object> onComplete, object payload)
        {
            if (_b is null)
            {
                Assert.IsNull(_op, "Operation should be null when result is set");
                onComplete(this, _result, payload);
                return;
            }

            _b.OnComplete.Add(onComplete, payload);
        }
    }
}