using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets
{
    [Serializable]
    public class AssetGroup
    {
        [ShowIf("@" + nameof(AddressableCatalog) + "." + nameof(AddressableCatalog.EditNameEnabled))]
        public string BundleName;
        [LabelText("@BundleName"), TableList(ShowPaging = false)]
        [OnValueChanged(nameof(Entries_OnValueChanged), includeChildren: true)]
        public AssetEntry[] Entries;

        [SerializeField, HideInInspector] internal string GeneratorId;
        public bool IsGenerated => !string.IsNullOrEmpty(GeneratorId);

        public AssetGroup(string bundleName, AssetEntry[] entries)
        {
            BundleName = bundleName;
            Entries = entries;

            foreach (var entry in Entries)
            {
                var path = AssetDatabase.GUIDToAssetPath(entry.GUID.Value);
                if (string.IsNullOrEmpty(path))
                    throw new ArgumentException($"Asset not found: address={entry.Address}, guid={entry.GUID.Value}");
            }
        }

        // single entry AssetGroup
        public AssetGroup(string bundleName, AssetEntry entry)
            : this(bundleName, new[] { entry }) { }

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
                assetBundleName = BundleName,
                assetNames = Entries.Select(e => AssetDatabase.GetAssetPath(e.Asset)).ToArray(),
                addressableNames = Entries.Select(e => AddressUtils.Hash(e.Address).Name()).ToArray()
            };
        }

        private void Entries_OnValueChanged()
        {
            _cachedAddressToAssetMap = null;
        }
    }
}