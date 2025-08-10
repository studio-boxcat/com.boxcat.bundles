using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Bundles.Editor
{
    public partial class AssetCatalog
    {
        private Dictionary<GroupKey, AssetGroup> _cachedGroupKeyToGroupMap;
        private Dictionary<AssetBundleId, AssetGroup> _cachedBundleIdToGroupMap;
        private Dictionary<Address, AssetEntry> _cachedAddressToEntryMap;
        private Dictionary<GUID, AssetEntry> _cachedAssetGUIDToEntryMap;
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

        public bool TryGetEntry(GUID guid, out AssetEntry entry)
        {
            ref var cache = ref _cachedAssetGUIDToEntryMap;
            if (cache is null)
            {
                cache = new Dictionary<GUID, AssetEntry>(Groups.Length * 64);
                foreach (var assetGroup in Groups)
                foreach (var assetEntry in assetGroup.Entries)
                    cache.Add(assetEntry.GUID, assetEntry);
                cache.TrimExcess();
            }

            return cache.TryGetValue(guid, out entry);
        }

        public AssetEntry GetEntry(GUID guid)
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
                foreach (var e in TraverseEntries_AddressAccess())
                    cache.Add(e.Hash, e);
                cache.TrimExcess();
            }

            return cache.TryGetValue(address, out entry);
        }

        public AssetEntry GetEntry(Address address)
        {
            return TryGetEntry(address, out var entry) ? entry
                : throw new KeyNotFoundException($"Entry with address '{address.Name()}' not found.");
        }

        public AssetEntry GetEntry(AssetLocation loc)
        {
            var group = GetGroup(loc.BundleId);
            return group[loc.AssetIndex];
        }

        public bool ContainsEntry(Address address) => TryGetEntry(address, out _);

        public string[] GetAddressList()
        {
            if (_cachedAddressList is not null)
                return _cachedAddressList;

            var cache = TraverseEntries_AddressAccess().Select(x => x.Address).ToArray();
            Array.Sort(cache, StringComparer.Ordinal);
            return _cachedAddressList = cache;
        }

        private void ClearCache()
        {
            L.I("[AssetCatalog] ClearCache");
            _cachedGroupKeyToGroupMap = null;
            _cachedBundleIdToGroupMap = null;
            _cachedAddressToEntryMap = null;
            _cachedAssetGUIDToEntryMap = null;
            _cachedAddressList = null;
        }
    }
}