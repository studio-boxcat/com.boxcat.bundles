using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets
{
    public partial class AddressableCatalog : ScriptableObject
    {
        [FormerlySerializedAs("AssetGroups")]
        [SerializeField, HideInInspector]
        public AssetGroup[] Groups;

        [ShowInInspector, LabelText("Groups"), HideReferenceObjectPicker]
        [ListDrawerSettings(DraggableItems = false, ShowPaging = false, ShowFoldout = false)]
        [OnValueChanged(nameof(ClearCache), includeChildren: true)]
        [CustomContextMenu("Toggle Edit Name", nameof(ToggleEditName))]
        public AssetGroup[] NormalGroups
        {
            get => Groups
                .Where(x => !x.IsGenerated)
                .ToArray();
            set => Groups = value.Concat(Groups.Where(x => x.IsGenerated)).ToArray();
        }

        [ShowInInspector, LabelText("Generated Groups"), ReadOnly, HideReferenceObjectPicker]
        [ListDrawerSettings(DefaultExpandedState = false, DraggableItems = false, ShowPaging = false)]
        public AssetGroup[] GeneratedGroups => Groups
            .Where(x => x.IsGenerated)
            .ToArray();

        public AssetGroup DefaultGroup => Groups[0];

        private Dictionary<string, AssetGroup> _cachedGroupNameToGroupMap;
        private Dictionary<string, AssetEntry> _cachedAddressToEntryMap;
        private Dictionary<string, AssetEntry> _cachedAssetGUIDToEntryMap;
        [NonSerialized] private List<string> _cachedAddressList;

        public bool TryGetGroup(string groupName, out AssetGroup group)
        {
            _cachedGroupNameToGroupMap ??= Groups.ToDictionary(x => x.Name, x => x);
            return _cachedGroupNameToGroupMap.TryGetValue(groupName, out group);
        }

        public AssetGroup GetGroup(string groupName)
        {
            if (TryGetGroup(groupName, out var group)) return group;
            throw new KeyNotFoundException($"Group '{groupName}' not found.");
        }

        public AssetEntry GetEntryByGUID(AssetGUID guid)
        {
            ref var cache = ref _cachedAssetGUIDToEntryMap;
            if (cache is not null)
                return cache[guid.Value];

            cache = new Dictionary<string, AssetEntry>(Groups.Length * 64);
            foreach (var assetGroup in Groups)
            foreach (var assetEntry in assetGroup.Entries)
                cache.Add(assetEntry.GUID.Value, assetEntry);
            cache.TrimExcess();
            return cache[guid.Value];
        }

        public AssetEntry GetEntryByAddress(string address)
        {
            ref var cache = ref _cachedAddressToEntryMap;
            if (cache is not null)
                return cache[address];

            cache = new Dictionary<string, AssetEntry>(Groups.Length * 64);
            foreach (var assetGroup in Groups)
            foreach (var assetEntry in assetGroup.Entries)
            {
                if (!string.IsNullOrEmpty(assetEntry.Address))
                    cache.Add(assetEntry.Address, assetEntry);
            }
            cache.TrimExcess();
            return cache[address];
        }

        public List<string> GetAddressList()
        {
            ref var cache = ref _cachedAddressList;
            if (cache is not null)
                return cache;

            cache = new List<string>(Groups.Length * 64);
            foreach (var assetGroup in Groups)
            foreach (var assetEntry in assetGroup.Entries)
            {
                var address = assetEntry.Address;
                if (!string.IsNullOrEmpty(address))
                    cache.Add(address);
            }
            cache.TrimExcess();
            cache.Sort();
            return cache;
        }

        private void ClearCache()
        {
            L.I("[AddressableCatalog] ClearCache");
            _cachedGroupNameToGroupMap = null;
            _cachedAssetGUIDToEntryMap = null;
            _cachedAddressList = null;
        }

        internal static bool EditNameEnabled;
        private static void ToggleEditName() => EditNameEnabled = !EditNameEnabled;

        [Button, PropertyOrder(-1)]
        private void GenerateGroups()
        {
            var groups = Groups.Where(x => !x.IsGenerated).ToList(); // keep normal groups

            var generatedGroups = new List<AssetGroup>();
            var methods = TypeCache.GetMethodsWithAttribute<AssetGroupGeneratorAttribute>();
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<AssetGroupGeneratorAttribute>();
                var generatorId = attr.GeneratorId;
                var oldCount = generatedGroups.Count;
                method.Invoke(null, parameters: new object[] { generatedGroups });
                for (var i = oldCount; i < generatedGroups.Count; i++)
                    generatedGroups[i].GeneratorId = generatorId;
            }

            generatedGroups.Sort((x, y) =>
            {
                var cmp = string.CompareOrdinal(x.GeneratorId, y.GeneratorId);
                return cmp != 0 ? cmp : string.CompareOrdinal(x.Name, y.Name);
            });

            groups.AddRange(generatedGroups);
            Groups = groups.ToArray();
            ClearCache();
        }
    }
}