using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets
{
    [Serializable]
    public class AssetGroup : ISelfValidator
    {
        [SerializeField, ShowIf("EditMode"), Delayed]
        private string _key;
        public GroupKey Key => new(_key);

        [LabelText("$Entries_LabelText"), PropertyOrder(100)]
        [TableList(ShowPaging = true, DrawScrollView = false)]
        [ListDrawerSettings(DraggableItems = false)]
        [OnValueChanged(nameof(Entries_OnValueChanged), includeChildren: true)]
        public AssetEntry[] Entries;

        [HideInInspector]
        public AssetBundleId BundleId;

        [FormerlySerializedAs("GeneratorId")]
        [SerializeField, HideInInspector] internal string GeneratorName;
        public bool IsGenerated => !string.IsNullOrEmpty(GeneratorName);

        [SerializeField, ShowIf("EditMode"), DisplayAsString]
        internal string LastDependency;

        public AssetGroup(string key, AssetEntry[] entries)
        {
            _key = key;
            Entries = entries;
        }

        public AssetEntry this[AssetIndex index]
        {
            get
            {
                Assert.IsTrue(IsGenerated, "This group is not generated.");

                // If the entry at the index has same address, return it
                var i = (int) index;
                var count = Entries.Length;
                if (i < count)
                {
                    var e = Entries[i];
                    if (e.Address == i.ToStringSmallNumber())
                        return e;
                }

                // Binary search
                var l = 0;
                var r = count - 1;
                while (l <= r)
                {
                    var m = (l + r) / 2;
                    var e = Entries[m];
                    if (e.Address == i.ToStringSmallNumber()) return e;
                    if (i < int.Parse(e.Address)) r = m - 1;
                    else l = m + 1;
                }

                throw new IndexOutOfRangeException($"Entry with index '{i}' not found in group '{_key}'.");
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

        internal void Internal_AddEntries(AssetGUID[] guids)
        {
            var entries = new AssetEntry[guids.Length];
            for (var index = 0; index < guids.Length; index++)
            {
                var guid = guids[index];
                var path = AssetDatabase.GUIDToAssetPath(guid.Value);
                entries[index] = new AssetEntry(guid) { HintName = Path.GetFileName(path) };
            }

            Entries = Entries.CloneConcat(entries);
            _cachedAddressToAssetMap = null;
        }

        public void Internal_RemoveEntries(AssetGUID[] guids)
        {
            var guidsSet = new HashSet<AssetGUID>(guids);
            Entries = Entries.Where(e => !guidsSet.Contains(e.GUID)).ToArray();
            _cachedAddressToAssetMap = null;
        }

        internal void SortEntries()
        {
            Array.Sort(Entries, (a, b) =>
            {
                var hasAddressA = !string.IsNullOrEmpty(a.Address);
                var hasAddressB = !string.IsNullOrEmpty(b.Address);
                if (hasAddressA != hasAddressB)
                    return hasAddressA ? -1 : 1;

                var pathA = a.ResolveAssetPath();
                var pathB = b.ResolveAssetPath();
                return string.CompareOrdinal(pathA, pathB);
            });

            // no need to clear cache, as the address is not changed
        }

        internal AssetBundleBuild GenerateAssetBundleBuild()
        {
            var assetBundleName = BundleId.Name();
            var assetNames = Entries.Select(e => e.ResolveAssetPath()).ToArray();
            var addressableNames = Entries.Select(e => ResolveAddressString(this, e)).ToArray();

            L.I(string.Format(
                $"[AddressableCatalog] Build {assetBundleName} ({_key}):\n"
                + string.Join("\n", addressableNames.Select((x, i) => $"{x} -> {assetNames[i]}"))));

            return new AssetBundleBuild
            {
                assetBundleName = assetBundleName,
                assetNames = assetNames,
                addressableNames = addressableNames,
            };
        }

        private string Entries_LabelText()
        {
            return EditMode()
                ? "Entries"
                : $"{_key} ({BundleId.Name()})";
        }

        private void Entries_OnValueChanged()
        {
            _cachedAddressToAssetMap = null;
        }

        private static bool EditMode() => AddressableCatalog.EditMode;

        void ISelfValidator.Validate(SelfValidationResult result)
        {
            if (BundleId is AssetBundleId.MonoScript)
                result.AddError("MonoScript is reserved for built-in MonoScript bundles.");

            if (IsGenerated)
            {
                var lastIndex = int.MinValue;
                foreach (var e in Entries)
                {
                    var index = int.Parse(e.Address);
                    if (index <= lastIndex) result.AddError($"The address '{e.Address}' is not unique. Please assign a unique address.");
                    lastIndex = index;
                }
            }
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