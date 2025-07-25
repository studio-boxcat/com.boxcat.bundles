using Sirenix.OdinInspector.Editor.Validation;

[assembly: RegisterValidator(typeof(Bundles.Editor.AddressValidator))]

namespace Bundles.Editor
{
    internal class AddressValidator : ValueValidator<Address>
    {
        protected override void Validate(Sirenix.OdinInspector.Editor.Validation.ValidationResult result)
        {
            var value = ValueEntry.SmartValue;
            if (value == default)
            {
                result.AddError("Address is not set.");
                return;
            }

            var catalog = AssetCatalog.Default;
            if (catalog.ContainsEntry(value) is false)
            {
                result.AddError($"Given Address is not registered: {value.Name()}");
                return;
            }
        }
    }
}