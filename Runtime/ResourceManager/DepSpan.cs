using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Represents a "view" over the portion of the catalog's dependency array
    /// corresponding to one bundle. Each dependency is stored as a 2-byte (ushort) value,
    /// but in the new schema those values are "canonical indices".
    /// Thus, when reading them, you may need to map them to "raw" AssetBundleId
    /// via the array of AssetBundleIds[] stored in the catalog.
    /// </summary>
    internal readonly struct DepSpan
    {
        private readonly byte[] _data; // The entire catalog data buffer
        private readonly ushort _pointer; // Index in the ushort[] portion of the entire buffer
        private readonly ushort _count; // How many dependencies

        public DepSpan(byte[] data, ushort pointer, ushort count)
        {
            _data = data;
            _pointer = pointer;
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

        public override unsafe string ToString()
        {
            if (_count == 0) return string.Empty;

            var list = new string[_count];
            fixed (byte* b = _data)
            {
                var p = (AssetBundleIndex*) (b + _pointer);
                for (int i = 0; i < _count; i++)
                {
                    var val = p[i];
                    list[i] = val.DebugString();
                }
            }
            return string.Join(", ", list);
        }

        internal unsafe AssetBundleIndex this[int index]
        {
            get
            {
                Assert.IsNotNull(_data, "Span not initialized");
                Assert.IsTrue(index >= 0 && index < _count, "Index out of range");
                fixed (byte* b = _data)
                {
                    var p = (AssetBundleIndex*) (b + _pointer);
                    return p[index];
                }
            }
        }

        public unsafe bool Contains(AssetBundleIndex index)
        {
            Assert.IsNotNull(_data, "Span not initialized");
            fixed (byte* b = _data)
            {
                var p = (AssetBundleIndex*) (b + _pointer);
                for (int i = 0; i < _count; i++)
                {
                    if (p[i] == index)
                        return true;
                }
            }
            return false;
        }
    }
}