using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
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

        private Dictionary<GroupKey, AssetGroup> _cachedGroupNameToGroupMap;
        private Dictionary<string, AssetEntry> _cachedAddressToEntryMap;
        private Dictionary<string, AssetEntry> _cachedAssetGUIDToEntryMap;
        [NonSerialized] private List<string> _cachedAddressList;

        public bool TryGetGroup(GroupKey groupKey, out AssetGroup group)
        {
            _cachedGroupNameToGroupMap ??= Groups.ToDictionary(x => x.Key, x => x);
            return _cachedGroupNameToGroupMap.TryGetValue(groupKey, out group);
        }

        public AssetGroup GetGroup(GroupKey groupKey)
        {
            if (TryGetGroup(groupKey, out var group)) return group;
            throw new KeyNotFoundException($"Group '{groupKey}' not found.");
        }

        public bool TryGetEntryByGUID(AssetGUID guid, out AssetEntry entry)
        {
            ref var cache = ref _cachedAssetGUIDToEntryMap;
            if (cache is null)
            {
                cache = new Dictionary<string, AssetEntry>(Groups.Length * 64);
                foreach (var assetGroup in Groups)
                foreach (var assetEntry in assetGroup.Entries)
                    cache.Add(assetEntry.GUID.Value, assetEntry);
                cache.TrimExcess();
            }

            return cache.TryGetValue(guid.Value, out entry);
        }

        public AssetEntry GetEntryByGUID(AssetGUID guid)
        {
            return TryGetEntryByGUID(guid, out var entry) ? entry
                : throw new KeyNotFoundException($"Entry with GUID '{guid}' not found.");
        }

        public bool TryGetEntryByAddress(string address, out AssetEntry entry)
        {
            ref var cache = ref _cachedAddressToEntryMap;
            if (cache is null)
            {
                cache = new Dictionary<string, AssetEntry>(Groups.Length * 64);
                foreach (var assetGroup in Groups)
                foreach (var assetEntry in assetGroup.Entries)
                {
                    if (!string.IsNullOrEmpty(assetEntry.Address))
                        cache.Add(assetEntry.Address, assetEntry);
                }
                cache.TrimExcess();
            }

            return cache.TryGetValue(address, out entry);
        }

        public AssetEntry GetEntryByAddress(string address)
        {
            return TryGetEntryByAddress(address, out var entry) ? entry
                : throw new KeyNotFoundException($"Entry with address '{address}' not found.");
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

        internal AssetBundleBuild[] GenerateBundleBuilds()
        {
            var builds = new AssetBundleBuild[Groups.Length];
            for (var i = 0; i < Groups.Length; i++)
                builds[i] = Groups[i].GenerateAssetBundleBuild();
            return builds;
        }

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
                return cmp != 0 ? cmp : string.CompareOrdinal(x.Key.Value, y.Key.Value);
            });

            groups.AddRange(generatedGroups);
            Groups = groups.ToArray();
            ClearCache();
        }

        [Button, PropertyOrder(-1)]
        private void AssignBundleId()
        {
            var bundleId = AssetBundleId.BuiltInShader + 1;
            foreach (var group in Groups)
                group.BundleId = bundleId++;
        }

        internal static bool EditNameEnabled;
        private static void ToggleEditName() => EditNameEnabled = !EditNameEnabled;
    }
}