using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Bundles.Editor
{
    public partial class AssetCatalog : ISelfValidator
    {
        void ISelfValidator.Validate(SelfValidationResult result)
        {
            // Check for duplicate bundle id
            var bundleIdToGroup = new Dictionary<AssetBundleId, AssetGroup>();
            foreach (var group in Groups)
            {
                if (bundleIdToGroup.TryGetValue(group.BundleId, out var orgGroup))
                {
                    result.AddError($"Duplicate bundle id: {orgGroup.Key} and {group.Key} have the same bundle id: {group.BundleId.Name()}");
                }
                else
                {
                    bundleIdToGroup.Add(group.BundleId, group);
                }
            }

            // Check for duplicate asset guids
            var assetSet = new Dictionary<string, AssetGroup>(); // asset guid to string address
            foreach (var group in Groups)
            foreach (var entry in group.Entries)
            {
                var guid = entry.GUID.Value;
                // empty guid will be reported by AssetEntry
                if (string.IsNullOrEmpty(guid))
                    continue;
                if (assetSet.TryAdd(guid, group) is false)
                    result.AddError($"Duplicate asset guid: {assetSet[guid].Key} and {group.Key} have the same asset: {entry.ResolveAssetPath()}");
            }

            // Check for duplicate addresses
            var addressHashToStr = new Dictionary<Address, string>(); // hash address to string address
            foreach (var entry in TraverseEntries_AddressAccess()) // check only bundles with address access.
            {
                var hash = AddressUtils.Hash(entry.Address);
                if (addressHashToStr.TryGetValue(hash, out var orgStr))
                {
                    result.AddError($"Duplicate address: {orgStr} and {entry.Address} have the same hash: {hash.Hex()}");
                }
                else
                {
                    addressHashToStr.Add(hash, entry.Address);
                }
            }
        }
    }
}