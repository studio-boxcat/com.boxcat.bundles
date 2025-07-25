#nullable enable
using System;
using System.Diagnostics;

namespace Bundles
{
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field)]
    public class AssetAddressAttribute : Attribute
    {
        public readonly Type? Type;

        public AssetAddressAttribute(Type type) => Type = type;
    }
}