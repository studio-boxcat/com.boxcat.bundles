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
        private Dictionary<Address, AssetEntry> _cachedAddressToEntryMap;
        private Dictionary<string, AssetEntry> _cachedAssetGUIDToEntryMap;
        [NonSerialized] private string[] _cachedAddressList;

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
            return bundleId is AssetBundleId.MonoScript
                ? (GroupKey) bundleId.ToString() // Catalog doesn't have a group for these built-in bundles.
                : GetGroup(bundleId).Key;
        }

        public bool TryGetEntry(AssetGUID guid, out AssetEntry entry)
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

        public AssetEntry GetEntry(AssetGUID guid)
        {
            return TryGetEntry(guid, out var entry) ? entry
                : throw new KeyNotFoundException($"Entry with GUID '{guid}' not found.");
        }

        public bool TryGetEntry(Address address, out AssetEntry entry)
        {
            ref var cache = ref _cachedAddressToEntryMap;
            if (cache is null)
            {
                cache = new Dictionary<Address, AssetEntry>(Groups.Length * 64, AddressComparer.Instance);
                foreach (var g in Groups.Where(x => x.BundleId.AddressAccess()))
                foreach (var e in g.Entries)
                {
                    if (!string.IsNullOrEmpty(e.Address))
                        cache.Add(AddressUtils.Hash(e.Address), e);
                }
                cache.TrimExcess();
            }

            return cache.TryGetValue(address, out entry);
        }

        public AssetEntry GetEntry(Address address)
        {
            return TryGetEntry(address, out var entry) ? entry
                : throw new KeyNotFoundException($"Entry with address '{address.Name()}' not found.");
        }

        public bool ContainsEntry(Address address) => TryGetEntry(address, out _);
        public bool ContainsEntry(string address) => TryGetEntry(AddressUtils.Hash(address), out _);

        public string[] GetAddressList()
        {
            if (_cachedAddressList is not null)
                return _cachedAddressList;

            var cache = new List<string>(Groups.Length * 64);
            foreach (var group in Groups.Where(x => x.BundleId.AddressAccess()))
            foreach (var entry in group.Entries)
            {
                var address = entry.Address;
                if (!string.IsNullOrEmpty(address))
                    cache.Add(address);
            }
            cache.Sort();
            return _cachedAddressList = cache.ToArray();
        }

        private void ClearCache()
        {
            L.I("[AddressableCatalog] ClearCache");
            _cachedGroupKeyToGroupMap = null;
            _cachedBundleIdToGroupMap = null;
            _cachedAddressToEntryMap = null;
            _cachedAssetGUIDToEntryMap = null;
            _cachedAddressList = null;
        }
    }
}