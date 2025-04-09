using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets
{
    internal readonly struct IndexToId
    {
        private readonly byte[] _data;
        private readonly int _start;

        public IndexToId(byte[] data, int start)
        {
            _data = data;
            _start = start;
        }

        public unsafe AssetBundleId this[AssetBundleIndex index]
        {
            get
            {
                Assert.IsNotNull(_data, "Span not initialized");
                Assert.IsTrue(index.Value() * 2 + _start < _data.Length,
                    $"Index out of bounds: ({index.Value()} * 2 + {_start}) >= {_data.Length}");

                fixed (byte* b = _data)
                {
                    var d = (ushort*) (b + _start); // interpret entire _data as an array of ushorts
                    return (AssetBundleId) d[index.Value()]; // 2 bytes per AssetBundleId
                }
            }
        }
    }
}