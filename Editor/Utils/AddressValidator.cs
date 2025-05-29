using System;
using JetBrains.Annotations;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.Validation;
using UnityEditor;
using UnityEngine;

[assembly: RegisterValidator(typeof(Bundles.Editor.AddressValidator))]

namespace Bundles.Editor
{
    [UsedImplicitly]
    internal class AddressDrawer : OdinValueDrawer<Address>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var value = ValueEntry.SmartValue;
            var catalog = AddressableCatalog.Default;
            var list = catalog.GetAddressList();
            var index = catalog.TryGetEntry(value, out var entry)
                ? Array.IndexOf(list, entry.Address) : -1;
            EditorGUILayout.Popup(label, index, list);
        }
    }

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

            var catalog = AddressableCatalog.Default;
            if (catalog.ContainsEntry(value) is false)
            {
                result.AddError($"Given Address is not registered: {value.Name()}");
                return;
            }
        }
    }
}