using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    public partial class AddressableCatalog : ISelfValidator
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
            foreach (var group in Groups.Where(x => x.BundleId.AddressAccess())) // check only bundles with address access.
            foreach (var entry in group.Entries)
            {
                if (string.IsNullOrEmpty(entry.Address))
                    continue;

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