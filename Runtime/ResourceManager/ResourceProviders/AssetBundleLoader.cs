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

            // Load the MonoScript asset bundle immediately.
            _bundles[AssetBundleId.MonoScript.Index()]
                = ReadAssetBundle(AssetBundleId.MonoScript);
        }

        public AssetBundle GetResolvedBundle(AssetBundleId id)
        {
            var bundle = _bundles[id.Index()];
            Assert.IsNotNull(bundle, "AssetBundle not found");
            Assert.AreEqual(AssetBundleResolveContext.Done, _contexts[id.Index()], "AssetBundle not fully resolved");
            return bundle;
        }

        public bool TryGetResolvedBundle(AssetBundleId bundleId, out AssetBundle bundle)
        {
            var bundleIndex = bundleId.Index();
            var ctx = _contexts[bundleIndex];

            // Never resolved.
            if (ctx is null)
            {
                bundle = null;
                return false;
            }

            // Fully resolved.
            if (ReferenceEquals(ctx, AssetBundleResolveContext.Done))
            {
                bundle = _bundles[bundleIndex];
                Assert.IsNotNull(bundle, "AssetBundle not found");
                return true;
            }

            // Currently resolving.
            bundle = null;
            return false;
        }

        public bool ResolveAsync(AssetBundleId bundleId, AssetBundleSpan deps, object payload, Action<AssetBundle, object> callback)
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
            L.I($"[AssetBundleLoader] ResolveAsync: {bundleId.Name()}, deps: {deps.Count}");
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

        public AssetBundle ResolveImmediate(AssetBundleId bundleId, AssetBundleSpan deps)
        {
            var bundleIndex = bundleId.Index();
            var ctx = _contexts[bundleIndex];

            // Fully resolved.
            if (ReferenceEquals(ctx, AssetBundleResolveContext.Done))
            {
                var bundle = _bundles[bundleIndex];
                Assert.IsNotNull(bundle, "AssetBundle not found");
                return bundle;
            }

            // Currently resolving.
            if (ctx is not null)
            {
                CompleteResolveImmediate(bundleId);
                Assert.AreEqual(AssetBundleResolveContext.Done, _contexts[bundleIndex], "AssetBundle not fully resolved");
                var bundle = _bundles[bundleIndex];
                Assert.IsNotNull(bundle, "AssetBundle not found");
                return bundle;
            }

            // Never resolved.

            L.I($"[AssetBundleLoader] ResolveImmediate: {bundleId.Name()}, deps: {deps.Count}");

            // First, load the dependent asset bundles.
            var depCount = deps.Count;
            for (var i = 0; i < depCount; i++)
                LoadBundle(deps[i]);

            // Then, load the target asset bundle itself.
            return LoadBundle(bundleId);


            AssetBundle LoadBundle(AssetBundleId bundleId)
            {
                var bundleIndex = bundleId.Index();
                var bundle = _bundles[bundleIndex];

                // No need to load at all.
                if (bundle is not null)
                {
                    return bundle;
                }
                // If the dependent asset bundle is currently loading, complete it immediately.
                else if (_requests.TryGetValue(bundleId, out var data))
                {
                    var (req, _) = data;
                    bundle = req.WaitForComplete();
                    Assert.IsNotNull(_bundles[bundleId.Index()], "AssetBundle not found");
                    Assert.IsFalse(_reqToBundle.ContainsKey(req), "AssetBundleCreateRequest not removed");
                    return bundle;
                }
                // Otherwise, load it immediately.
                else
                {
                    // No need to deal with the context as it will be fully resolved immediately.
                    bundle = ReadAssetBundle(bundleId);
                    _bundles[bundleId.Index()] = bundle;
                    return bundle;
                }
            }
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
            var req = ReadAssetBundleAsync(bundleId);
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

        public void CompleteResolveImmediate(AssetBundleId bundleId)
        {
            var bundleIndex = bundleId.Index();
            var ctx = _contexts[bundleIndex];
            Assert.IsNotNull(ctx, "AssetBundle is not resolving");

            // If the asset bundle is already fully resolved, OnLoaded() will be manually called.
            if (ReferenceEquals(ctx, AssetBundleResolveContext.Done))
            {
                Assert.IsNotNull(_bundles[bundleIndex], "AssetBundle not found");
                return;
            }

            // When we access assetBundle property,
            // there could be multiple asset bundles loaded at the same time.
            // So here we use while loop instead of for loop.
            var reqs = ctx.Reqs;
            reqs.Add(null); // Add a dummy request to prevent reentrancy.
            Assert.AreNotEqual(0, reqs.Count, "AssetBundle not loading");
            while (true)
            {
                var count = reqs.Count;
                if (count is 1) break; // Contains only the dummy request.

                var req = reqs[count - 2]; // Last request is the dummy request.
                Assert.IsTrue(_reqToBundle.ContainsKey(req), "AssetBundleCreateRequest not found");
                _ = req.WaitForComplete();

                Assert.AreEqual(bundleId, ctx.BundleId, "AssetBundleResolveContext is recycled while resolving");
                Assert.IsFalse(_reqToBundle.ContainsKey(req), "AssetBundleCreateRequest not removed");
                Assert.IsTrue(reqs.Count < count, "AssetBundleCreateRequest not removed");
            }

            // All requests must be done.
            Assert.AreEqual(1, reqs.Count, "AssetBundle not fully loaded");
            Assert.IsNull(reqs[0], "Only the dummy request should be left");
            Assert.AreEqual(ctx, _contexts[bundleIndex], "AssetBundleResolveContext is recycled while resolving");
            reqs.Clear();
            OnResolved(ctx);

            Assert.AreEqual(AssetBundleResolveContext.Done, _contexts[bundleIndex], "AssetBundle not fully resolved");
            Assert.IsFalse(_requests.Values.Any(x => x.Requesters.Contains(ctx)), "AssetBundleResolveContext still in the list");
        }

        readonly Action<AsyncOperation> _onLoaded;

        void OnLoaded(AsyncOperation op)
        {
            Assert.IsTrue(op.isDone, "Operation is not done");
            var req = (AssetBundleCreateRequest) op;
            // First, set the asset bundle to the array to prevent reentrancy.
            var found = _reqToBundle.Remove(req, out var bundleId);
            Assert.IsTrue(found, "AssetBundleCreateRequest not found");

            var bundle = req.assetBundle;
            if (bundle is null)
            {
                L.E($"[AssetBundleLoader] OnLoaded: {bundleId.Name()} failed");
                bundle = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(x => x.name == bundleId.Name());
                if (bundle is null)
                    throw new Exception($"AssetBundleCreateRequest failed: {bundleId.Name()}");
            }

            _bundles[bundleId.Index()] = bundle;
            L.I($"[AssetBundleLoader] OnLoaded: {bundleId.Name()} ({bundle.name} assets)");

            // Get requesters.
            found = _requests.Remove(bundleId, out var data);
            Assert.IsTrue(found, "AssetBundleCreateRequest not found");
            Assert.AreEqual(req, data.Request, "AssetBundleCreateRequest not matched");
            var requesters = data.Requesters;
            Assert.AreNotEqual(0, requesters.Count, "No requester found");

            // Remove the request from the requesters.
            foreach (var ctx in requesters)
            {
                found = ctx.Reqs.Remove(req);
                Assert.IsTrue(found, "AssetBundleCreateRequest not found in the requester.");

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
            L.I($"[AssetBundleLoader] OnResolved: {ctx.BundleId.Name()}");

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

        static AssetBundle ReadAssetBundle(AssetBundleId bundleId)
        {
            var path = PathConfig.GetAssetBundleLoadPath(bundleId);
            var bundle = AssetBundle.LoadFromFile(path);
            Assert.IsNotNull(bundle, "AssetBundle failed to load: " + bundleId.Name());
            L.I($"[AssetBundleLoader] ReadAssetBundle: {bundleId.Name()} ({bundle.name})\n" +
                $"assets: {string.Join(", ", bundle.GetAllAssetNames())}");
            return bundle;
        }

        static AssetBundleCreateRequest ReadAssetBundleAsync(AssetBundleId bundleId)
        {
            var path = PathConfig.GetAssetBundleLoadPath(bundleId);
            var op = AssetBundle.LoadFromFileAsync(path);
            Assert.IsNotNull(op, "AssetBundleCreateRequest not found");
            L.I($"[AssetBundleLoader] ReadAssetBundleAsync: {bundleId.Name()}");
#if DEBUG
            _debug_AllRequests.Add(op);
            op.completed += op =>
            {
                _debug_AllRequests.Remove((AssetBundleCreateRequest) op);

                var bundle = ((AssetBundleCreateRequest) op).assetBundle;
                if (bundle is null)
                {
                    L.W($"[AssetBundleLoader] ReadAssetBundleAsync: {bundleId.Name()} failed");
                    return;
                }

                L.I($"[AssetBundleLoader] ReadAssetBundleAsync: {bundleId.Name()} ({bundle.name}) completed\n" +
                    $"assets: {string.Join(", ", ((AssetBundleCreateRequest) op).assetBundle.GetAllAssetNames())}");
            };
#endif
            return op;
        }

#if DEBUG
        static readonly List<AssetBundleCreateRequest> _debug_AllRequests = new();

        public static void Debug_UnloadAllAssetBundles()
        {
            // Before unload all asset bundles, wait for all async operations to complete.
            // (AssetBundle.Unload was called while the asset bundle had an async load operation in progress. The main thread will wait for the async load operation to complete.)
            var reqs = _debug_AllRequests;
            while (reqs.Count > 0)
            {
                var req = reqs[0];
                reqs.RemoveAt(0);
                req.WaitForComplete();
            }

            AssetBundle.UnloadAllAssetBundles(true);
        }
#endif

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