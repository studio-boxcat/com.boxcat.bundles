using System;

namespace UnityEditor.AddressableAssets
{
    [Serializable]
    public struct GroupKey : IEquatable<GroupKey>, IComparable<GroupKey>
    {
        public readonly string Value;

        public GroupKey(string value) => Value = value;

        public override string ToString() => Value;

        public bool Equals(GroupKey other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GroupKey other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(GroupKey other) => string.CompareOrdinal(Value, other.Value);

        public static bool operator ==(GroupKey x, GroupKey y) => x.Equals(y);
        public static bool operator !=(GroupKey x, GroupKey y) => !x.Equals(y);

        public static explicit operator string(GroupKey value) => value.Value;
        public static explicit operator GroupKey(string value) => new(value);
    }
}