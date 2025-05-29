using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.Validation;
using UnityEditor;
using UnityEngine;

[assembly: RegisterValidator(typeof(Bundles.Editor.AssetAddressValidator))]

namespace Bundles.Editor
{
    internal sealed class AssetAddressValidator : AttributeValidator<AssetAddressAttribute>
    {
        protected override void Validate(Sirenix.OdinInspector.Editor.Validation.ValidationResult result)
        {
            if (TryResolveAddress(Property.ValueEntry, out var address) is false)
                return;

            var catalog = AssetCatalog.Default;
            if (!catalog.TryGetEntry(address, out var assetEntry))
            {
                result.AddError($"Given Address is not registered: {address.Name()}");
                return;
            }

            var expectedType = Attribute.Type;
            if (expectedType != null)
            {
                var assetType = assetEntry.ResolveAssetType();
                if (assetType.IsAssignableFrom(expectedType))
                    return;

                if (expectedType == typeof(Sprite) && assetType == typeof(Texture2D))
                {
                    var ti = AssetImporter.GetAtPath(assetEntry.ResolveAssetPath()) as TextureImporter;
                    if (ti != null && ti.textureType == TextureImporterType.Sprite) return;
                }

                result.AddError($"Given Address is not of type {expectedType.Name}: {address.Name()} ({assetType.Name})");
            }
            return;

            static bool TryResolveAddress(IPropertyValueEntry valueEntry, out Address address)
            {
                switch (valueEntry)
                {
                    case IPropertyValueEntry<string> e:
                    {
                        var value = e.SmartValue;
                        address = AddressUtils.Hash(value);
                        return true;
                    }
                    case IPropertyValueEntry<Address> e:
                        address = e.SmartValue;
                        return true;
                    default:
                        address = default;
                        return false;
                }
            }
        }
    }
}