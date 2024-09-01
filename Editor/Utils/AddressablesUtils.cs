using System.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    public static class AddressablesUtils
    {
        public static bool TryGetAssetByAddress(this AddressableAssetGroup group, string name, out Object asset)
        {
            var entry = group.entries.FirstOrDefault(x => x.address == name);
            if (entry != null)
            {
                asset = entry.MainAsset;
                return true;
            }
            else
            {
                asset = default;
                return false;
            }
        }
    }
}