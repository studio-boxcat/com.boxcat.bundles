using System.Collections.Generic;
using System.Linq;

namespace Bundles.Editor
{
    internal static class AddressCodeGen
    {
        public static void GenerateCode(AssetCatalog catalog, string path)
        {
            var list = new List<(string Name, Address Value, string Comment)>();
            foreach (var g in catalog.Groups.Where(g => g.BundleId.AddressAccess()))
            {
                var groupName = g.Key.Value;
                var bundleId = g.BundleId.Name();
                foreach (var e in g.Entries.Where(e => e.Address.NotEmpty()))
                {
                    var addressName = e.Address;
                    var address = AddressUtils.Hash(addressName);
                    // b.EnumValue(addressName, $"{(uint) address}u", comment: comment);
                    list.Add((addressName, address, $"0x{address.Hex()}, guid={e.GUID.Value}, group={groupName}, bundleId={bundleId}"));
                }
            }
            list.Sort((a, b) => a.Name.CompareToOrdinal(b.Name));

            var b = new CodeBuilder();

            using (b.Enum("Address", "uint"))
            {
                foreach (var (name, value, comment) in list)
                    b.EnumValue(name, $"{(uint) value}u", comment: comment);
            }

            b.WriteTo(path);
        }
    }
}