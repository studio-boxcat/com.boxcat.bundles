using System;
using JetBrains.Annotations;

namespace UnityEditor.AddressableAssets
{
    [AttributeUsage(AttributeTargets.Method), MeansImplicitUse]
    public class AssetGroupGeneratorAttribute : Attribute
    {
        public readonly string GeneratorId;

        public AssetGroupGeneratorAttribute(string generatorId)
        {
            GeneratorId = generatorId;
        }
    }
}