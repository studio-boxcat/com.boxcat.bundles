using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets
{
    [Serializable]
    public class AssetGroup : ISelfValidator
    {
        [ShowIf("@" + nameof(AddressableCatalog) + "." + nameof(AddressableCatalog.EditNameEnabled))]
        [SerializeField]
        private string _key;
        public GroupKey Key => new(_key);

        [LabelText("$Entries_LabelText")]
        [TableList(ShowIndexLabels = true, ShowPaging = false)]
        [OnValueChanged(nameof(Entries_OnValueChanged), includeChildren: true)]
        public AssetEntry[] Entries;

        [HideInInspector]
        public AssetBundleId BundleId;

        [SerializeField, HideInInspector] internal string GeneratorId;
        public bool IsGenerated => !string.IsNullOrEmpty(GeneratorId);

        public AssetGroup(string key, AssetEntry[] entries)
        {
            _key = key;
            Entries = entries;
        }

        public AssetEntry this[AssetIndex index] => Entries[(int) index];

        private Dictionary<string, Object> _cachedAddressToAssetMap;

        public bool TryGetAssetByAddress(string address, out Object asset)
        {
            _cachedAddressToAssetMap ??= Entries
                .Where(e => !string.IsNullOrEmpty(e.Address))
                .ToDictionary(e => e.Address, e => e.Asset);
            return _cachedAddressToAssetMap.TryGetValue(address, out asset);
        }

        public AssetBundleBuild GenerateAssetBundleBuild()
        {
            var assetNames = Entries.Select(e => AssetDatabase.GUIDToAssetPath((GUID) e.GUID)).ToArray();
            var addressableNames = Entries.Select(e => ResolveAddressString(this, e)).ToArray();

            return new AssetBundleBuild
            {
                assetBundleName = _key,
                assetNames = assetNames,
                addressableNames = addressableNames,
            };
        }

        private string Entries_LabelText()
        {
            return $"{_key} ({BundleId.Name()})";
        }

        private void Entries_OnValueChanged()
        {
            _cachedAddressToAssetMap = null;
        }

        void ISelfValidator.Validate(SelfValidationResult result)
        {
            if (BundleId is AssetBundleId.MonoScript)
                result.AddError("MonoScript is reserved for built-in MonoScript bundles.");
        }

        [CanBeNull]
        internal static string ResolveAddressString(AssetGroup g, AssetEntry e)
        {
            if (string.IsNullOrEmpty(e.Address)) return null;
            return g.BundleId.AddressAccess()
                ? AddressUtils.Hash(e.Address).Name()
                : e.Address;
        }

        [CanBeNull]
        internal static Address? ResolveAddressNumeric(AssetGroup g, AssetEntry e)
        {
            if (string.IsNullOrEmpty(e.Address)) return null;
            return g.BundleId.AddressAccess()
                ? AddressUtils.Hash(e.Address)
                : (Address) int.Parse(e.Address);
        }
    }
}