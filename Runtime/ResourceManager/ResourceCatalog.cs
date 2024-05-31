using System.Collections.Generic;
using System.Linq;
using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets
{
    public readonly struct AssetBundleSpan
    {
        readonly byte[] _data;
        readonly ushort _start;
        readonly ushort _count;

        public AssetBundleSpan(byte[] data, ushort start, ushort count)
        {
            _data = data;
            _start = start;
            _count = count;
        }

        public int Count
        {
            get
            {
                Assert.IsNotNull(_data, "Span not initialized");
                return _count;
            }
        }

        public override string ToString()
        {
            return string.Join(", ", _data.Skip(_start).Take(_count).Select(i => ((AssetBundleId) i).Name()));
        }

        public AssetBundleId this[int index]
        {
            get
            {
                Assert.IsNotNull(_data, "Span not initialized");
                Assert.IsTrue(index >= 0 && index < _count, "Index out of range");
                return (AssetBundleId) _data[_start + index];
            }
        }

        public bool Contains(AssetBundleId bundle)
        {
            Assert.IsNotNull(_data, "Span not initialized");
            var bundleId = bundle.Index();
            for (var i = 0; i < _count; i++)
                if (_data[_start + i] == bundleId)
                    return true;
            return false;
        }
    }

    public readonly struct ResourceCatalog
    {
        // AssetBundleCount: ushort
        // ResourceLocationCount: ushort
        // AssetBundleDepSpans: uint[]
        //     Start: ushort
        //     Count: ushort
        // ResourceLocations: uint[] (Sorted by Address)
        //     AssetBundle: 1 byte
        //     Address: 3 bytes
        // AssetBundleDepData: byte[]
        readonly byte[] _data;
        readonly int _bundleCount;
        readonly int _locStart; // uint*
        readonly int _locEnd; // uint*


        public unsafe ResourceCatalog(byte[] data)
        {
            _data = data;
            fixed (byte* b = data)
            {
                var d = (ushort*) b;
                _bundleCount = d[0]; // AssetBundleCount (ushort)
                var locCount = d[1]; // ResourceLocationCount (ushort).
                _locStart = 1 // AssetBundleCount (ushort) + ResourceLocationCount (ushort) -> 1 uint
                            + _bundleCount; // AssetBundleDepSpans (uint[]).
                _locEnd = _locStart + locCount;
            }
        }

        public int GetBundleCount() => _bundleCount;

        public unsafe AssetBundleId GetContainingBundle(Address address)
        {
            // ResourceLocation[]: uint[] (Sorted by Address)
            //     AssetBundle: MSB 1 byte
            //     Address: LSB 3 bytes

            fixed (byte* b = _data)
            {
                var d = (uint*) b;
                var l = _locStart;
                var r = _locEnd;
                var addr = (uint) address;

                while (l < r)
                {
                    var m = (l + r) / 2;
                    var cur = d[m];
                    var a = cur & 0x00FFFFFF;
                    if (a < addr) l = m + 1;
                    else if (a > addr) r = m;
                    else return (AssetBundleId) (cur >> 24);
                }
            }

            throw new KeyNotFoundException("Address not found in ResourceCatalog: " + address.ReadableString());
        }

        public unsafe AssetBundleSpan GetDependencies(AssetBundleId bundle)
        {
            // AssetBundleDepSpans: uint[]
            //     Start: ushort
            //     Count: ushort

            fixed (byte* b = _data)
            {
                var d = (ushort*) b
                        + 2 // Skip AssetBundleCount (ushort) and ResourceLocationCount (ushort).
                        + (int) bundle * 2; // Skip to the start of the span.
                return new AssetBundleSpan(_data, d[0], d[1]);
            }
        }
    }
}