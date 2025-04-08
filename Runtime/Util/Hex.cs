using System.Runtime.CompilerServices;
using UnityEngine.Assertions;

namespace UnityEngine.AddressableAssets.Util
{
    internal static class Hex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char Char(uint value)
        {
            Assert.IsTrue(value is >= 0 and < 16, "value is out of range");
            return (char) (value < 10 ? (value + '0') : (value - 10 + 'a'));
        }

        private static readonly char[] _hexBuf = new char[8];

        public static unsafe string To4(ushort value)
        {
            // https://stackoverflow.com/a/624379
            fixed (char* p = _hexBuf)
            {
                var v = (uint) value;
                p[0] = Char(v >> 12);
                p[1] = Char((v >> 8) & 0xF);
                p[2] = Char((v >> 4) & 0xF);
                p[3] = Char(v & 0xF);
            }
            return new string(_hexBuf, 0, 4);
        }

        public static unsafe void To4(ushort value, char[] chars, int startIndex)
        {
            // https://stackoverflow.com/a/624379
            fixed (char* p = chars)
            {
                var v = (uint) value;
                p[startIndex] = Char(v >> 12);
                p[startIndex + 1] = Char((v >> 8) & 0xF);
                p[startIndex + 2] = Char((v >> 4) & 0xF);
                p[startIndex + 3] = Char(v & 0xF);
            }
        }

        public static unsafe string To8(uint value)
        {
            // https://stackoverflow.com/a/624379
            fixed (char* p = _hexBuf)
            {
                p[0] = Char(value >> 28);
                p[1] = Char((value >> 24) & 0xF);
                p[2] = Char((value >> 20) & 0xF);
                p[3] = Char((value >> 16) & 0xF);
                p[4] = Char((value >> 12) & 0xF);
                p[5] = Char((value >> 8) & 0xF);
                p[6] = Char((value >> 4) & 0xF);
                p[7] = Char(value & 0xF);
            }
            return new string(_hexBuf, 0, 8);
        }
    }
}