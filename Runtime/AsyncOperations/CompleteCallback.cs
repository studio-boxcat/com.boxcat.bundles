#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Assertions;

namespace Bundles
{
    internal readonly struct CompleteCallback
    {
        private readonly List<Callback> _callbacks;


        public static CompleteCallback Create()
        {
            return new CompleteCallback(new List<Callback>());
        }

        private CompleteCallback(List<Callback> callbacks)
        {
            _callbacks = callbacks;
        }

        public bool IsEmpty => _callbacks.Count == 0;

        public void Add(object callback, bool op) => _callbacks.Add(new Callback(callback, op));
        public void Add(object callback, bool op, object payload) => _callbacks.Add(new Callback(callback, op, payload));
        public void Add(object callback, bool op, int payload) => _callbacks.Add(new Callback(callback, op, payload));
        public void Add(object callback, bool op, object payloadObj, int payloadInt) => _callbacks.Add(new Callback(callback, op, payloadObj, payloadInt));

        public void Invoke<TResult>(IAssetOp<TResult> op, TResult result)
        {
            Assert.IsNotNull(_callbacks);

            foreach (var callback in _callbacks)
            {
                try
                {
                    callback.Invoke(op, result);
                }
                catch (Exception e)
                {
                    L.E(e);
                }
            }

            _callbacks.Clear();
        }

        private readonly struct Callback
        {
            private readonly object _delegate;
            // 2^3 = 8 conventions (optional: TAssetOp, object, int)
            // 0: Action<TResult>
            // 1: Action<TResult, object>
            // 2: Action<TResult, int>
            // 3: Action<TResult, object, int>
            // 4: Action<IAssetOp<TResult>, TResult>
            // 5: Action<IAssetOp<TResult>, TResult, object>
            // 6: Action<IAssetOp<TResult>, TResult, int>
            // 7: Action<IAssetOp<TResult>, TResult, object, int>
            private readonly byte _convention;
            private readonly object? _payloadObj;
            private readonly int _payloadInt;

            public Callback(object @delegate, bool op) : this()
            {
                _delegate = @delegate;
                _convention = Convention(0, op);
            }

            public Callback(object @delegate, bool op, object payloadObj) : this()
            {
                _delegate = @delegate;
                _convention = Convention(1, op);
                _payloadObj = payloadObj;
            }

            public Callback(object @delegate, bool op, int payloadInt) : this()
            {
                _delegate = @delegate;
                _convention = Convention(2, op);
                _payloadInt = payloadInt;
            }

            public Callback(object @delegate, bool op, object payloadObj, int payloadInt) : this()
            {
                _delegate = @delegate;
                _convention = Convention(3, op);
                _payloadObj = payloadObj;
                _payloadInt = payloadInt;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte Convention(byte convention, bool op)
            {
                return op ? (byte) (convention + 4) : convention;
            }

            public void Invoke<TResult>(IAssetOp<TResult> op, TResult result)
            {
                switch (_convention)
                {
                    // no op
                    case 0:
                        ((Action<TResult>) _delegate)(result);
                        break;
                    case 1:
                        ((Action<TResult, object?>) _delegate)(result, _payloadObj);
                        break;
                    case 2:
                        ((Action<TResult, int>) _delegate)(result, _payloadInt);
                        break;
                    case 3:
                        ((Action<TResult, object?, int>) _delegate)(result, _payloadObj, _payloadInt);
                        break;
                    // with op
                    case 4:
                        ((Action<IAssetOp<TResult>, TResult>) _delegate)(op, result);
                        break;
                    case 5:
                        ((Action<IAssetOp<TResult>, TResult, object?>) _delegate)(op, result, _payloadObj);
                        break;
                    case 6:
                        ((Action<IAssetOp<TResult>, TResult, int>) _delegate)(op, result, _payloadInt);
                        break;
                    case 7:
                        ((Action<IAssetOp<TResult>, TResult, object?, int>) _delegate)(op, result, _payloadObj, _payloadInt);
                        break;
                    default:
                        L.W($"Unknown callback convention: {_convention.StrSmall()}");
                        break;
                }
            }
        }
    }
}