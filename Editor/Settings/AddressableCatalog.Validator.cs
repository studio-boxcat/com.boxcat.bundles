using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    public partial class AddressableCatalog : ISelfValidator
    {
        void ISelfValidator.Validate(SelfValidationResult result)
        {
            var dict = new Dictionary<Address, string>(); // hash address to string address

            foreach (var group in Groups)
            foreach (var entry in group.Entries)
            {
                if (string.IsNullOrEmpty(entry.Address))
                    continue;

                var hash = AddressUtils.Hash(entry.Address);
                if (dict.TryGetValue(hash, out var orgStr))
                {
                    result.AddError($"Duplicate address: {orgStr} and {entry.Address} have the same hash: {hash.Name()}");
                }
                else
                {
                    dict.Add(hash, entry.Address);
                }
            }
        }
    }
}