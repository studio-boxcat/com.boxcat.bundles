using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    public partial class AddressableCatalog
    {
        private Dictionary<GroupKey, AssetGroup> _cachedGroupKeyToGroupMap;
        private Dictionary<AssetBundleId, AssetGroup> _cachedBundleIdToGroupMap;
        private Dictionary<string, AssetEntry> _cachedAddressToEntryMap;
        private Dictionary<string, AssetEntry> _cachedAssetGUIDToEntryMap;
        [NonSerialized] private List<string> _cachedAddressList;

        public bool TryGetGroup(GroupKey groupKey, out AssetGroup group)
        {
            _cachedGroupKeyToGroupMap ??= Groups.ToDictionary(x => x.Key, x => x);
            return _cachedGroupKeyToGroupMap.TryGetValue(groupKey, out group);
        }

        public AssetGroup GetGroup(AssetBundleId bundleId)
        {
            _cachedBundleIdToGroupMap ??= Groups.ToDictionary(x => x.BundleId, x => x);
            return _cachedBundleIdToGroupMap[bundleId];
        }

        public GroupKey ResolveGroupKeyForDisplay(AssetBundleId bundleId)
        {
            return bundleId is AssetBundleId.MonoScript or AssetBundleId.BuiltInShaders
                ? (GroupKey) bundleId.ToString() // Catalog doesn't have a group for these built-in bundles.
                : GetGroup(bundleId).Key;
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
                foreach (var assetGroup in Groups.Where(x => x.BundleId.AddressAccess()))
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
            if (cache is null)
                return cache;

            cache = new List<string>(Groups.Length * 64);
            foreach (var assetGroup in Groups.Where(x => x.BundleId.AddressAccess()))
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
            _cachedGroupKeyToGroupMap = null;
            _cachedBundleIdToGroupMap = null;
            _cachedAssetGUIDToEntryMap = null;
            _cachedAddressList = null;
        }
    }
}