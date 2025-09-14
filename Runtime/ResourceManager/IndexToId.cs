using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace Bundles
{
    internal readonly struct IndexToId
    {
        private readonly NativeArray<byte>.ReadOnly _data;
        private readonly int _start;

        public IndexToId(NativeArray<byte>.ReadOnly data, int start)
        {
            _data = data;
            _start = start;
        }

        public unsafe AssetBundleId this[AssetBundleIndex index]
        {
            get
            {
                Assert.IsTrue(_data.IsCreated, "Span not initialized");
                Assert.IsTrue(index.Val() * 2 + _start < _data.Length,
                    $"Index out of bounds: ({index.Val()} * 2 + {_start}) >= {_data.Length}");

                var b = (byte*) _data.GetUnsafeReadOnlyPtr();
                var d = (ushort*) (b + _start); // interpret entire _data as an array of ushorts
                return (AssetBundleId) d[index.Val()]; // 2 bytes per AssetBundleId
            }
        }
    }
}