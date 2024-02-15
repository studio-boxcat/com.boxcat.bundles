using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.AddressableAssets.Util;

namespace UnityEngine.AddressableAssets.AsyncOperations
{
    public class AssetOpBlock
    {
        public Address Address;
        public AssetBundleId Bundle;
        public IResourceProvider Provider;

        public readonly ResourceCatalog Catalog;
        public readonly AssetBundleLoader Loader;
        public readonly CompleteCallback OnComplete;

        readonly List<AssetOpBlock> _pool;


        public AssetOpBlock(ResourceCatalog catalog, AssetBundleLoader loader, List<AssetOpBlock> pool)
        {
            Catalog = catalog;
            Loader = loader;
            OnComplete = new CompleteCallback(new List<(object, object)>());
            _pool = pool;
        }

        public void Init(Address address, AssetBundleId bundle, IResourceProvider provider)
        {
            Address = address;
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
        AssetOpBlock _b;
        AsyncOperation _op;
        TResult _result;


        public AssetOp(AssetOpBlock b)
        {
            _b = b;

            // If the main asset bundle is already fully loaded, we can start the asset load operation immediately.
            var bundleId = _b.Bundle;
            if (_b.Loader.TryGetResolvedBundle(bundleId, out var bundle))
            {
                OnDepLoaded(bundle);
                return;
            }

            // Possible to be resolved while StartResolve() is called.
            // In this case, callbackRegistered will be false, so we can call OnDepLoaded immediately.
            var deps = _b.Catalog.GetDependencies(bundleId);
            var callbackRegistered = _b.Loader.StartResolve(bundleId, deps, this, _onDepLoaded);
            if (callbackRegistered is false)
                OnDepLoaded(_b.Loader.GetResolvedBundle(bundleId));
        }

        public bool IsDone
        {
            get
            {
                // If result is already loaded, return true.
                if (_b is null)
                {
                    Assert.IsNull(_op, "Operation should be null when result is set");
                    return true;
                }

                // If dep is not loaded, _op will be null.
                if (_op is null || _op.isDone is false)
                    return false;

                // If OnComplete is not yet called but operation is done, call it.
                ForceOnComplete(_op);
                Assert.IsNull(_op, "Operation should be null after completion");
                Assert.IsNull(_b, "Block should be null after completion");
                return true;
            }
        }

        public TResult Result
        {
            get
            {
                // If result is already loaded, return it.
                if (_b is null)
                    return _result;

                // If dep is not loaded, _op will be null.
                // In this case, load dependencies immediately.
                if (_op is null)
                {
                    _b.Loader.CompleteResolveImmediate(_b.Bundle);
                    Assert.IsNotNull(_op, "OnDepLoaded() should have been called after dependencies are loaded");
                }

                // Wait for operation to be done.
                _op.WaitForComplete();

                // Manually call OnComplete
                ForceOnComplete(_op);
                Assert.IsNull(_op, "Operation should be null after completion");
                Assert.IsNull(_b, "Block should be null after completion");

                return _result;
            }
        }

        public void AddOnComplete(Action<TResult> onComplete)
        {
            if (_b is null)
            {
                Assert.IsNull(_op, "Operation should be null when result is set");
                onComplete.SafeInvoke(_result);
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

        static readonly Action<AssetBundle, object> _onDepLoaded = static (bundle, thiz) => ((AssetOp<TResult>) thiz).OnDepLoaded(bundle);

        void OnDepLoaded(AssetBundle bundle)
        {
            Assert.IsNull(_op, "Operation should be null before execution");
            Assert.IsNotNull(_b, "Block should not be null before execution");

            _op = _b.Provider.Execute(bundle, _b.Address);

            if (_op.isDone)
            {
                OnComplete(_op);
                return;
            }

            AsyncOpPayloads.SetData(_op, this);
            _op.completed += _onComplete;
        }

        static readonly Action<AsyncOperation> _onComplete = static op =>
        {
            var thiz = AsyncOpPayloads.PopData(op);
            Assert.AreEqual(typeof(AssetOp<TResult>), thiz.GetType(),
                $"Payload mismatch: expected={typeof(AssetOp<TResult>)}, actual={thiz.GetType()}");
            ((AssetOp<TResult>) thiz).OnComplete(op);
        };

        void ForceOnComplete(AsyncOperation req)
        {
            var payload = AsyncOpPayloads.PopData(req);
            Assert.AreEqual(this, payload, $"Payload mismatch: payload={payload?.GetType()}, this={GetType()}");
            req.completed -= _onComplete; // Prevent double call
            OnComplete(req);
        }

        void OnComplete(AsyncOperation req)
        {
            Assert.IsNotNull(_op, "OnComplete is called twice");
            Assert.AreEqual(req, _op, "Operation mismatch");
            Assert.IsTrue(_op.isDone, "Operation is not done");

            var b = _b;
            _b = null;
            var op = _op;
            _op = null;

            _result = (TResult) b.Provider.GetResult(op);
            b.OnComplete.Invoke(_result);
            b.Return();
        }

#if DEBUG
        public string GetDebugName()
        {
            if (_b is null) return "AssetOp:" + _result + " (Loaded)";
            return "AssetOp:" + _b.Address.ReadableString() + " (Loading)";
        }
#endif
    }
}