using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets
{
    [Serializable]
    public class AssetGroup : ISelfValidator
    {
        [FormerlySerializedAs("_name")]
        [FormerlySerializedAs("BundleName")]
        [FormerlySerializedAs("Name")]
        [ShowIf("@" + nameof(AddressableCatalog) + "." + nameof(AddressableCatalog.EditNameEnabled))]
        public string _key;
        public GroupKey Key => new(_key);

        [LabelText("@_key"), TableList(ShowPaging = false)]
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

            foreach (var entry in Entries)
            {
                var path = AssetDatabase.GUIDToAssetPath(entry.GUID.Value);
                if (string.IsNullOrEmpty(path))
                    throw new ArgumentException($"Asset not found: address={entry.Address}, guid={entry.GUID.Value}");
            }
        }

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
            return new AssetBundleBuild
            {
                assetBundleName = _key,
                assetNames = Entries.Select(e => AssetDatabase.GetAssetPath(e.Asset)).ToArray(),
                addressableNames = Entries.Select(e => AddressUtils.Hash(e.Address).Name()).ToArray()
            };
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
    }
}