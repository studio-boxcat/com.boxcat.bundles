using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Assertions;
using UnityEngine.AddressableAssets.Util;

namespace UnityEngine.AddressableAssets.ResourceProviders
{
    public class AssetBundleLoader
    {
        class AssetBundleResolveContext
        {
            // If context reference equal to this, it means the asset bundle is fully resolved.
            public static readonly AssetBundleResolveContext Done = new();

            public AssetBundleId BundleId;
            public List<AssetBundleCreateRequest> Reqs;
            public List<(Action<AssetBundle, object>, object)> Callbacks;
        }

        readonly AssetBundle[] _bundles;
        readonly AssetBundleResolveContext[] _contexts;
        readonly Dictionary<AssetBundleId, (AssetBundleCreateRequest Request, List<AssetBundleResolveContext> Requesters)> _requests = new();
        readonly Dictionary<AssetBundleCreateRequest, AssetBundleId> _reqToBundle = new();
        readonly List<AssetBundleResolveContext> _contextPool = new();
        readonly List<List<AssetBundleResolveContext>> _contextListPool = new();


        public AssetBundleLoader(int bundleCount)
        {
            _bundles = new AssetBundle[bundleCount];
            _contexts = new AssetBundleResolveContext[bundleCount];
            _onLoaded = OnLoaded;
        }

        public AssetBundle GetResolvedBundle(AssetBundleId id)
        {
            var bundle = _bundles[id.Index()];
            Assert.IsNotNull(bundle, "AssetBundle not found");
            Assert.AreEqual(AssetBundleResolveContext.Done, _contexts[id.Index()], "AssetBundle not fully resolved");
            return bundle;
        }

        public bool TryGetResolvedBundle(AssetBundleId id, out AssetBundle bundle)
        {
            var i = id.Index();
            var ctx = _contexts[i];

            // Never resolved.
            if (ctx is null)
            {
                bundle = null;
                return false;
            }

            // Fully resolved.
            if (ReferenceEquals(ctx, AssetBundleResolveContext.Done))
            {
                bundle = _bundles[i];
                Assert.IsNotNull(bundle, "AssetBundle not found");
                return true;
            }

            // Currently resolving.
            bundle = null;
            return false;
        }

        public bool StartResolve(AssetBundleId bundleId, AssetBundleSpan deps, object payload, Action<AssetBundle, object> callback)
        {
            // When the asset bundle is currently resolving...
            var bundleIndex = bundleId.Index();
            var ctx = _contexts[bundleIndex];
            if (ctx is not null)
            {
                Assert.AreNotEqual(AssetBundleResolveContext.Done, ctx, "Please use TryGetResolved() before calling StartResolve()");
                Assert.AreNotEqual(0, ctx.Reqs.Count, "At least one request is in progress");
                Assert.AreNotEqual(0, ctx.Callbacks.Count, "Callback list empty");
                ctx.Callbacks.Add((callback, payload));
                return true;
            }


            // Create a new context.
            L.I($"[AssetBundleLoader] Load AssetBundle: {bundleId}, deps: {deps.Count}");
            ctx = RentResolveContext(bundleId);
            _contexts[bundleIndex] = ctx;

            // Request asset bundles.
            Assert.IsFalse(deps.Contains(bundleId), "AssetBundle depends on itself");
            var depCount = deps.Count;
            for (var i = 0; i < depCount; i++)
                LoadAssetBundle(deps[i], ctx);
            LoadAssetBundle(bundleId, ctx);

            // If there were no request made, consider as fully resolved.
            if (ctx.Reqs.Count is 0)
            {
                Assert.IsFalse(_requests.Values.Any(x => x.Requesters.Contains(ctx)), "AssetBundleResolveContext still in the list");
                _contexts[bundleIndex] = AssetBundleResolveContext.Done; // Mark as fully resolved.
                ReturnResolveContext(ctx);
                return false;
            }

            ctx.Callbacks.Add((callback, payload));
            return true;
        }

        void LoadAssetBundle(AssetBundleId bundleId, AssetBundleResolveContext ctx)
        {
            Assert.AreNotEqual(AssetBundleResolveContext.Done, ctx, "Invalid context");

            var bundleIndex = bundleId.Index();
            var bundle = _bundles[bundleIndex];

            // No need to load at all.
            if (bundle is not null)
                return;

            // When request already made, add the context to the list.
            if (_requests.TryGetValue(bundleId, out var data))
            {
                var (oldReq, oldRequesters) = data;
                Assert.IsFalse(oldRequesters.Contains(ctx), "AssetBundleResolveContext already in the list");
                oldRequesters.Add(ctx);
                Assert.IsFalse(ctx.Reqs.Contains(oldReq), "AssetBundleCreateRequest already in the list");
                ctx.Reqs.Add(oldReq);
                return;
            }

            // When request not made yet...
            var bundlePath = PathConfig.GetAssetBundleLoadPath(bundleId);
            var req = AssetBundle.LoadFromFileAsync(bundlePath);
            Assert.IsFalse(req.isDone, "AssetBundle already loaded");

            // Register request accordingly.
            var requesters = RentRequesters();
            requesters.Add(ctx);
            _requests.Add(bundleId, (req, requesters)); // Request -> Context
            _reqToBundle.Add(req, bundleId); // Request -> Bundle
            ctx.Reqs.Add(req); // Context -> Request

            // Finally, register the callback.
            // This should be the last operation to prevent reentrancy.
            req.completed += _onLoaded;
        }

        public void CompleteResolveImmediate(AssetBundleId bundle)
        {
            var bundleIndex = bundle.Index();
            var ctx = _contexts[bundleIndex];
            Assert.IsNotNull(ctx, "AssetBundle is not resolving");

            // If the asset bundle is already fully resolved, OnLoaded() will be manually called.
            if (ReferenceEquals(ctx, AssetBundleResolveContext.Done))
            {
                Assert.IsNotNull(_bundles[bundleIndex], "AssetBundle not found");
                return;
            }

            var reqs = ctx.Reqs;
            var reqCount = reqs.Count;
            Assert.AreNotEqual(0, reqCount, "AssetBundle not loading");

            // Intentionally iterate in reverse as the list will be modified in the OnLoaded().
            for (var i = reqCount - 1; i >= 0; i--)
            {
                var req = reqs[i];
                req.WaitForComplete(); // Wait for the request to be done.
                OnLoaded(req);
                Assert.AreEqual(i, reqs.Count, "AssetBundleCreateRequest not removed");
            }
            Assert.AreEqual(0, reqs.Count, "AssetBundle not fully loaded");

            Assert.AreEqual(AssetBundleResolveContext.Done, _contexts[bundleIndex], "AssetBundle not fully resolved");
            Assert.IsFalse(_requests.Values.Any(x => x.Requesters.Contains(ctx)), "AssetBundleResolveContext still in the list");
        }

        readonly Action<AsyncOperation> _onLoaded;

        void OnLoaded(AsyncOperation op)
        {
            Assert.IsTrue(op.isDone, "Operation is not done");

            var req = (AssetBundleCreateRequest) op;

            // When the ResolveImmediate() is called, OnLoaded() will be manually called.
            if (_reqToBundle.Remove(req, out var bundleId) is false)
            {
                Assert.IsFalse(_requests.ContainsKey(bundleId), "AssetBundleCreateRequest not removed");
                return;
            }

            // First, set the asset bundle to the array to prevent reentrancy.
            var bundle = req.assetBundle;
            Assert.IsNotNull(bundle, "AssetBundle not loaded");
            L.I($"[AssetBundleLoader] AssetBundle loaded: {bundleId} ({bundle.name})\n{string.Join(", ", bundle.GetAllAssetNames())}");
            _bundles[bundleId.Index()] = bundle;

            // Get requesters.
            var removed = _requests.Remove(bundleId, out var data);
            Assert.IsTrue(removed, "AssetBundleCreateRequest not found");
            Assert.AreEqual(req, data.Request, "AssetBundleCreateRequest not matched");
            var requesters = data.Requesters;
            Assert.AreNotEqual(0, requesters.Count, "No requester found");

            // Remove the request from the context.
            foreach (var ctx in requesters)
            {
                removed = ctx.Reqs.Remove(req);
                Assert.IsTrue(removed, "AssetBundleCreateRequest not found in the requester.");

                // If all requests are done, mark as fully resolved.
                if (ctx.Reqs.Count is 0)
                    OnResolved(ctx);
            }

            // Return the requesters to the pool.
            requesters.Clear();
            ReturnRequesters(requesters);
        }

        void OnResolved(AssetBundleResolveContext ctx)
        {
            Assert.AreNotEqual(AssetBundleResolveContext.Done, ctx, "AssetBundle already resolved");
            Assert.IsNotNull(ctx, "AssetBundleResolveContext not found");
            Assert.AreEqual(ctx.Reqs.Count, 0, "AssetBundle not fully loaded");
            Assert.IsFalse(_requests.Values.Any(x => x.Requesters.Contains(ctx)), "AssetBundleResolveContext still in the list");


            // Mark as fully resolved before invoking callbacks to ensure reentrancy.
            var bundleIndex = ctx.BundleId.Index();
            _contexts[bundleIndex] = AssetBundleResolveContext.Done;

            // Get the asset bundle.
            var bundle = _bundles[bundleIndex];
            Assert.IsNotNull(bundle, "AssetBundle not found");

            // Invoke all callbacks.
            var callbacks = ctx.Callbacks;
            Assert.AreNotEqual(0, callbacks.Count, "No callback to invoke");
            foreach (var (callback, payload) in callbacks)
            {
                try
                {
                    callback(bundle, payload);
                }
                catch (Exception e)
                {
                    L.Exception(e);
                }
            }
            callbacks.Clear();

            // Return the context to the pool.
            ReturnResolveContext(ctx);
        }

        AssetBundleResolveContext RentResolveContext(AssetBundleId bundleId)
        {
            var count = _contextPool.Count;
            if (count is 0)
            {
                return new AssetBundleResolveContext
                {
                    BundleId = bundleId,
                    Reqs = new List<AssetBundleCreateRequest>(),
                    Callbacks = new List<(Action<AssetBundle, object>, object)>()
                };
            }

            var ctx = _contextPool[count - 1];
            _contextPool.RemoveAt(count - 1);
            Assert.AreEqual(0, ctx.Reqs.Count, "AssetBundleResolveContext not reset");
            Assert.AreEqual(0, ctx.Callbacks.Count, "AssetBundleResolveContext not reset");

            ctx.BundleId = bundleId;
            return ctx;
        }

        void ReturnResolveContext(AssetBundleResolveContext ctx)
        {
            Assert.AreEqual(0, ctx.Reqs.Count, "AssetBundleResolveContext not reset");
            Assert.AreEqual(0, ctx.Callbacks.Count, "AssetBundleResolveContext not reset");
            _contextPool.Add(ctx);
        }

        List<AssetBundleResolveContext> RentRequesters()
        {
            var count = _contextListPool.Count;
            if (count is 0) return new List<AssetBundleResolveContext>();

            var list = _contextListPool[count - 1];
            _contextListPool.RemoveAt(count - 1);
            Assert.AreEqual(0, list.Count, "Requesters list not reset");
            return list;
        }

        void ReturnRequesters(List<AssetBundleResolveContext> list)
        {
            Assert.AreEqual(0, list.Count, "Requesters list not reset");
            _contextListPool.Add(list);
        }
    }
}