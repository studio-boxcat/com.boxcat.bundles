using JetBrains.Annotations;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEngine;

namespace Bundles.Editor
{
    [UsedImplicitly]
    internal class AssetAddressAttributeDrawer : OdinAttributeDrawer<AssetAddressAttribute>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (Property.ValueEntry is not IPropertyValueEntry<string> entry)
            {
                CallNextDrawer(label);
                return;
            }

            var addressList = AssetCatalog.Default.GetAddressList();
            var oldValue = entry.SmartValue;
            var newValue = SirenixEditorFields.Dropdown(label, oldValue, addressList);
            if (newValue != oldValue) entry.SmartValue = newValue;
        }
    }
}