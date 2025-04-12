using System;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    [Serializable]
    public struct AssetGUID : IEquatable<AssetGUID>, IComparable<AssetGUID>
    {
        public string Value;

        public AssetGUID(string value)
        {
            Value = value;
        }

        public bool IsValid() => !string.IsNullOrEmpty(Value);
        public bool IsInvalid() => string.IsNullOrEmpty(Value);

        public override string ToString() => Value;

        public bool Equals(AssetGUID other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AssetGUID other && Equals(other);
        public override int GetHashCode() => (Value != null ? Value.GetHashCode() : 0);
        public int CompareTo(AssetGUID other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

        public static bool operator ==(AssetGUID x, AssetGUID y) => x.Equals(y);
        public static bool operator !=(AssetGUID x, AssetGUID y) => !x.Equals(y);

        public static explicit operator AssetGUID(string value) => new(value);
        public static explicit operator AssetGUID(GUID value) => new(value.ToString());
        public static explicit operator GUID(AssetGUID value) => new(value.Value);
    }

    internal readonly struct EntryDef
    {
        public readonly AssetGUID GUID;
        public readonly Address? Address;
        public readonly AssetBundleId Bundle;
        public readonly HashSet<AssetBundleId> Dependencies;

        public EntryDef(AssetGUID guid, Address? address, AssetBundleId bundle, HashSet<AssetBundleId> dependencies)
        {
            GUID = guid;
            Address = address;
            Bundle = bundle;
            Dependencies = dependencies;
        }

        public override string ToString()
        {
            if (Address is null)
            {
                var name = System.IO.Path.GetFileName(AssetDatabase.GUIDToAssetPath(GUID.Value));
                return $"{name}  ({GUID}) > {Bundle}";
            }
            return $"{Address.Value.ReadableString()} ({GUID}) > {Bundle}";
        }
    }
}