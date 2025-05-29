using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace Bundles
{
    internal class AssetBundleLoader
    {
        private readonly AssetBundle[] _bundles; // indexed by AssetBundleIndex
        private readonly Job[] _jobs; // indexed by AssetBundleIndex
        private readonly IndexToId _indexToId;
        private readonly Dictionary<AssetBundleIndex, (AssetBundleCreateRequest Request, List<Job> Jobs)> _requests = new();
        private readonly Dictionary<AssetBundleCreateRequest, AssetBundleIndex> _reqToBundle = new();


        public AssetBundleLoader(int bundleCount, IndexToId indexToId)
        {
            _bundles = new AssetBundle[bundleCount];
            _jobs = new Job[bundleCount];
            _indexToId = indexToId;
            _onLoaded = OnLoaded;
        }

        public void LoadMonoScriptBundle()
        {
            _bundles[AssetBundleIndex.MonoScript.Value()]
                = ReadAssetBundle(AssetBundleId.MonoScript);
        }

        public AssetBundle GetResolvedBundle(AssetBundleIndex index)
        {
            var idx = index.Value();
            var bundle = _bundles[idx];
            Assert.IsNotNull(bundle, "AssetBundle not found");
            Assert.AreEqual(Job.Done, _jobs[idx], "AssetBundle not fully resolved");
            return bundle;
        }

        public bool TryGetResolvedBundle(AssetBundleIndex bundleIndex, out AssetBundle bundle)
        {
            var idx = bundleIndex.Value();
            var job = _jobs[idx];

            // Never resolved.
            if (job is null)
            {
                bundle = null;
                return false;
            }

            // Fully resolved.
            if (ReferenceEquals(job, Job.Done))
            {
                bundle = _bundles[idx];
                Assert.IsNotNull(bundle, "AssetBundle not found");
                return true;
            }

            // Currently resolving.
            bundle = null;
            return false;
        }

        public bool ResolveAsync(AssetBundleIndex bundleIndex, DepSpan deps, object payload, Action<AssetBundle, object> callback)
        {
            // When the asset bundle is currently resolving (rare-case)
            var idx = bundleIndex.Value();
            var job = _jobs[idx];
            if (job is not null)
            {
                Assert.AreNotEqual(Job.Done, job, "Please use TryGetResolved() before calling ResolveAsync()");
                Assert.AreNotEqual(0, job.Reqs.Count, "At least one request is in progress");
                Assert.AreNotEqual(0, job.Callbacks.Count, "Callback list empty");
                job.Callbacks.Add((callback, payload));
                return false;
            }

            L.I($"[AssetBundleLoader] ResolveAsync: bundleIndex={bundleIndex.DebugString()}, deps: {deps.ToString()}");

            // Create a new job.
            job = Job.Rent(bundleIndex);
            _jobs[idx] = job;

            // Request asset bundles.
            Assert.IsFalse(deps.Contains(bundleIndex), "AssetBundle depends on itself");
            var depCount = deps.Count;
            for (var i = 0; i < depCount; i++)
            {
                var dep = deps[i];
                Assert.AreNotEqual(AssetBundleIndex.MonoScript, dep,
                    "MonoScript asset bundle should be loaded immediately");
                QueueAssetBundleLoad(dep, job);
            }
            QueueAssetBundleLoad(bundleIndex, job);

            // If there were no request made, consider as fully resolved.
            if (job.Reqs.Count is 0)
            {
                Assert.IsFalse(_requests.Values.Any(x => x.Jobs.Contains(job)), "AssetBundleResolveContext still in the list");
                _jobs[idx] = Job.Done; // Mark as fully resolved.
                Job.Return(job);
                return true;
            }

            job.Callbacks.Add((callback, payload));
            return false;

            void QueueAssetBundleLoad(AssetBundleIndex bundleIndex, Job job)
            {
                Assert.AreNotEqual(Job.Done, job, "Invalid job");

                var bundle = _bundles[bundleIndex.Value()];

                // No need to load at all.
                if (bundle is not null)
                    return;

                // When request already made, add the job to the list.
                if (_requests.TryGetValue(bundleIndex, out var data))
                {
                    var (oldReq, oldRequesters) = data;
                    Assert.IsFalse(oldRequesters.Contains(job), "AssetBundleResolveContext already in the list");
                    oldRequesters.Add(job);
                    Assert.IsFalse(job.Reqs.Contains(oldReq), "AssetBundleCreateRequest already in the list");
                    job.Reqs.Add(oldReq);
                    return;
                }

                // When request not made yet...
                var bundleId = _indexToId[bundleIndex];
                var req = ReadAssetBundleAsync(bundleId);
                Assert.IsFalse(req.isDone, "AssetBundle already loaded");

                // Register request accordingly.
                var requesters = Job.RentList();
                requesters.Add(job);
                _requests.Add(bundleIndex, (req, requesters)); // Request -> Context
                _reqToBundle.Add(req, bundleIndex); // Request -> Bundle
                job.Reqs.Add(req); // Context -> Request

                // Finally, register the callback.
                // This should be the last operation to prevent reentrancy.
                req.completed += _onLoaded;
            }
        }

        public AssetBundle ResolveImmediate(AssetBundleIndex bundleIndex, DepSpan deps, IndexToId indexToId)
        {
            var idx = bundleIndex.Value();
            var job = _jobs[idx];

            // Fully resolved.
            if (ReferenceEquals(job, Job.Done))
            {
                var bundle = _bundles[idx];
                Assert.IsNotNull(bundle, "AssetBundle not found");
                return bundle;
            }

            // Currently resolving.
            if (job is not null)
            {
                CompleteResolveImmediate(bundleIndex);
                Assert.AreEqual(Job.Done, _jobs[idx], "AssetBundle not fully resolved");
                var bundle = _bundles[idx];
                Assert.IsNotNull(bundle, "AssetBundle not found");
                return bundle;
            }

            // Never resolved.
            L.I($"[AssetBundleLoader] ResolveImmediate: {bundleIndex.DebugString()}, deps: {deps.ToString()}");

            // First, load the dependent asset bundles.
            var depCount = deps.Count;
            for (var i = 0; i < depCount; i++)
            {
                var dep = deps[i];
                Assert.AreNotEqual(AssetBundleIndex.MonoScript, dep,
                    "MonoScript asset bundle should be loaded immediately");
                LoadBundle(dep);
            }

            // Then, load the target asset bundle itself.
            return LoadBundle(bundleIndex);


            AssetBundle LoadBundle(AssetBundleIndex bundleIndex)
            {
                var idx = bundleIndex.Value();
                var bundle = _bundles[idx];

                // No need to load at all.
                if (bundle is not null)
                {
                    return bundle;
                }
                // If the dependent asset bundle is currently loading, complete it immediately.
                else if (_requests.TryGetValue(bundleIndex, out var data))
                {
                    var (req, _) = data;
                    bundle = req.WaitForComplete();
                    Assert.IsNotNull(_bundles[idx], "AssetBundle not found");
                    Assert.IsFalse(_reqToBundle.ContainsKey(req), "AssetBundleCreateRequest not removed");
                    return bundle;
                }
                // Otherwise, load it immediately.
                else
                {
                    // No need to deal with the job as it will be fully resolved immediately.
                    var bundleId = indexToId[bundleIndex];
                    bundle = ReadAssetBundle(bundleId);
                    _bundles[idx] = bundle;
                    L.I($"[AssetBundleLoader] LoadBundle: {bundleId.Name()} ({bundle.name})");
                    return bundle;
                }
            }
        }

        public void CompleteResolveImmediate(AssetBundleIndex bundleIndex)
        {
            var idx = bundleIndex.Value();
            var job = _jobs[idx];
            Assert.IsNotNull(job, "AssetBundle is not resolving");

            // If the asset bundle is already fully resolved, OnLoaded() will be manually called.
            if (ReferenceEquals(job, Job.Done))
            {
                Assert.IsNotNull(_bundles[idx], "AssetBundle not found");
                return;
            }

            // When we access assetBundle property,
            // there could be multiple asset bundles loaded at the same time.
            // So here we use while loop instead of for loop.
            var reqs = job.Reqs;
            reqs.Add(null); // Add a dummy request to prevent reentrancy.
            Assert.AreNotEqual(0, reqs.Count, "AssetBundle not loading");
            while (true)
            {
                var count = reqs.Count;
                if (count is 1) break; // Contains only the dummy request.

                var req = reqs[count - 2]; // Last request is the dummy request.
                Assert.IsTrue(_reqToBundle.ContainsKey(req), "AssetBundleCreateRequest not found");
                _ = req.WaitForComplete();

                Assert.AreEqual(bundleIndex, job.BundleIndex, "AssetBundleResolveContext is recycled while resolving");
                Assert.IsFalse(_reqToBundle.ContainsKey(req), "AssetBundleCreateRequest not removed");
                Assert.IsTrue(reqs.Count < count, "AssetBundleCreateRequest not removed");
            }

            // All requests must be done.
            Assert.AreEqual(1, reqs.Count, "AssetBundle not fully loaded");
            Assert.IsNull(reqs[0], "Only the dummy request should be left");
            Assert.AreEqual(job, _jobs[idx], "AssetBundleResolveContext is recycled while resolving");
            reqs.Clear();
            OnResolved(job);

            Assert.AreEqual(Job.Done, _jobs[idx], "AssetBundle not fully resolved");
            Assert.IsFalse(_requests.Values.Any(x => x.Jobs.Contains(job)), "AssetBundleResolveContext still in the list");
        }

        private readonly Action<AsyncOperation> _onLoaded;

        private void OnLoaded(AsyncOperation op)
        {
            Assert.IsTrue(op.isDone, "Operation is not done");
            var req = (AssetBundleCreateRequest) op;
            // First, set the asset bundle to the array to prevent reentrancy.
            var found = _reqToBundle.Remove(req, out var bundleIndex);
            Assert.IsTrue(found, "AssetBundleCreateRequest not found");

            var bundle = req.assetBundle;
            if (bundle is null)
                throw new Exception($"AssetBundleCreateRequest failed: {bundleIndex.DebugString()}");

            _bundles[bundleIndex.Value()] = bundle;
            L.I($"[AssetBundleLoader] OnLoaded: {bundleIndex.DebugString()} ({bundle.name} assets)");

            // Get requesters.
            found = _requests.Remove(bundleIndex, out var data);
            Assert.IsTrue(found, "AssetBundleCreateRequest not found");
            Assert.AreEqual(req, data.Request, "AssetBundleCreateRequest not matched");
            var jobs = data.Jobs;
            Assert.AreNotEqual(0, jobs.Count, "No requester found");

            // Remove the request from the requesters.
            foreach (var job in jobs)
            {
                found = job.Reqs.Remove(req);
                Assert.IsTrue(found, "AssetBundleCreateRequest not found in the requester.");

                // If all requests are done, mark as fully resolved.
                if (job.Reqs.Count is 0)
                    OnResolved(job);
            }

            // Return the requesters to the pool.
            jobs.Clear();
            Job.Return(jobs);
        }

        private void OnResolved(Job job)
        {
            L.I($"[AssetBundleLoader] OnResolved: {job.BundleIndex.DebugString()}");

            Assert.AreNotEqual(Job.Done, job, "AssetBundle already resolved");
            Assert.IsNotNull(job, "AssetBundleResolveContext not found");
            Assert.AreEqual(job.Reqs.Count, 0, "AssetBundle not fully loaded");
            Assert.IsFalse(_requests.Values.Any(x => x.Jobs.Contains(job)), "AssetBundleResolveContext still in the list");


            // Mark as fully resolved before invoking callbacks to ensure reentrancy.
            var idx = job.BundleIndex.Value();
            _jobs[idx] = Job.Done;

            // Get the asset bundle.
            var bundle = _bundles[idx];
            Assert.IsNotNull(bundle, "AssetBundle not found");

            // Invoke all callbacks.
            var callbacks = job.Callbacks;
            Assert.AreNotEqual(0, callbacks.Count, "No callback to invoke");
            foreach (var (callback, payload) in callbacks)
            {
                try
                {
                    callback(bundle, payload);
                }
                catch (Exception e)
                {
                    L.E(e);
                }
            }
            callbacks.Clear();

            // Return the job to the pool.
            Job.Return(job);
        }

        private static AssetBundle ReadAssetBundle(AssetBundleId bundleId)
        {
            EditorUnloadAlreadyLoadedAssetBundle(bundleId.Name());

            var path = Paths.GetAssetBundleLoadPath(bundleId);
            var bundle = AssetBundle.LoadFromFile(path);
            Assert.IsNotNull(bundle, "AssetBundle failed to load: " + bundleId.Name());
            L.I($"[AssetBundleLoader] ReadAssetBundle: {bundleId.Name()} ({bundle.name})\n" +
                $"assets: {string.Join(", ", bundle.GetAllAssetNames())}");
            return bundle;
        }

        private static AssetBundleCreateRequest ReadAssetBundleAsync(AssetBundleId bundleId)
        {
            EditorUnloadAlreadyLoadedAssetBundle(bundleId.Name());

            L.I($"[AssetBundleLoader] ReadAssetBundleAsync: {bundleId.Name()}");
            var path = Paths.GetAssetBundleLoadPath(bundleId);
            var op = AssetBundle.LoadFromFileAsync(path);
            Assert.IsNotNull(op, "AssetBundleCreateRequest not found");
#if DEBUG
            op.completed += op =>
            {
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

        [Conditional("UNITY_EDITOR")]
        private static void EditorUnloadAlreadyLoadedAssetBundle(string name)
        {
            var bundle = AssetBundle.GetAllLoadedAssetBundles()
                .FirstOrDefault(x => x.name == name);
            if (bundle is null)
                return;

            L.E($"[AssetBundleLoader] Unload: {name}");
            try
            {
                bundle.Unload(true);
            }
            catch
            {
                // Ignore the exception.
                // InvalidOperationException: This method should not be used after the AssetBundle has been unloaded.
            }
        }

#if DEBUG
        public void Dispose()
        {
            // store reqBundles before unloading.
            var reqBundles = _requests.Keys.ToHashSet();

            // wait for all requests to complete.
            var reqs = _requests.Values.Select(x => x.Request).ToArray(); // Copy to prevent modifying the collection.
            foreach (var req in reqs)
            {
                if (!req.isDone)
                    req.WaitForComplete();
            }

            // unload all asset bundles.
            for (var i = 0; i < _bundles.Length; i++)
            {
                var bundle = _bundles[i];
                if (!bundle) continue;
                L.I($"[AssetBundleLoader] Unload: {bundle.name}");
                bundle.Unload(true);
                reqBundles.Remove((AssetBundleIndex) i);
            }

            // there are still some asset bundles loaded.
            var allBundles = AssetBundle.GetAllLoadedAssetBundles()
                .ToDictionary(x => x.name, x => x);
            foreach (var reqBundle in reqBundles)
            {
                var name = _indexToId[reqBundle].Name();
                if (allBundles.TryGetValue(name, out var bundle))
                {
                    L.W($"[AssetBundleLoader] Unload: {name} (still loaded)");

                    try
                    {
                        bundle.Unload(true);
                    }
                    catch
                    {
                        // Ignore the exception.
                        // InvalidOperationException: This method should not be used after the AssetBundle has been unloaded.
                    }
                }
            }
        }
#endif

        private class Job
        {
            public AssetBundleIndex BundleIndex;
            public List<AssetBundleCreateRequest> Reqs;
            public List<(Action<AssetBundle, object>, object)> Callbacks;


            // If job reference equal to this, it means the asset bundle is fully resolved.
            public static readonly Job Done = new();


            private static readonly List<Job> _jobPool = new();
            private static readonly List<List<Job>> _jobListPool = new();

            public static Job Rent(AssetBundleIndex bundleIndex)
            {
                var count = _jobPool.Count;
                if (count is 0)
                {
                    return new Job
                    {
                        BundleIndex = bundleIndex,
                        Reqs = new List<AssetBundleCreateRequest>(),
                        Callbacks = new List<(Action<AssetBundle, object>, object)>()
                    };
                }

                var job = _jobPool[count - 1];
                _jobPool.RemoveAt(count - 1);
                Assert.AreEqual(0, job.Reqs.Count, "AssetBundleResolveContext not reset");
                Assert.AreEqual(0, job.Callbacks.Count, "AssetBundleResolveContext not reset");

                job.BundleIndex = bundleIndex;
                return job;
            }

            public static void Return(Job job)
            {
                Assert.AreEqual(0, job.Reqs.Count, "AssetBundleResolveContext not reset");
                Assert.AreEqual(0, job.Callbacks.Count, "AssetBundleResolveContext not reset");
                _jobPool.Add(job);
            }

            public static List<Job> RentList()
            {
                var count = _jobListPool.Count;
                if (count is 0) return new List<Job>();

                var list = _jobListPool[count - 1];
                _jobListPool.RemoveAt(count - 1);
                Assert.AreEqual(0, list.Count, "Requesters list not reset");
                return list;
            }

            public static void Return(List<Job> list)
            {
                Assert.AreEqual(0, list.Count, "Requesters list not reset");
                _jobListPool.Add(list);
            }
        }
    }
}