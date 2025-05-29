using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace Bundles
{
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field)]
    public class AssetAddressAttribute : Attribute
    {
        [CanBeNull]
        public readonly Type Type;

        public AssetAddressAttribute() { }
        public AssetAddressAttribute(Type type) => Type = type;
    }
}