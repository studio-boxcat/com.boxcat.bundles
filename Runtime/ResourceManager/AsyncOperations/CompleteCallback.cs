using System;
using System.Collections.Generic;
using UnityEngine.AddressableAssets.Util;
using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets.AsyncOperations
{
    public readonly struct CompleteCallback
    {
        readonly List<(object, object)> _callbacks;


        public CompleteCallback(List<(object, object)> callbacks)
        {
            _callbacks = callbacks;
        }

        public bool IsEmpty => _callbacks.Count == 0;

        public void Add(object callback, object payload)
        {
            _callbacks.Add((callback, payload));
        }

        public void Invoke<TResult>(TResult result)
        {
            Assert.IsNotNull(_callbacks);

            foreach (var (callback, payload) in _callbacks)
            {
                try
                {
                    switch (callback)
                    {
                        case Action<TResult> action:
                            action(result);
                            break;
                        case Action<TResult, object> actionWithPayload:
                            actionWithPayload(result, payload);
                            break;
                        default:
                            L.W($"Unknown callback type: {callback.GetType()}");
                            break;
                    }
                }
                catch (Exception e)
                {
                    L.Exception(e);
                }
            }

            _callbacks.Clear();
        }
    }
}