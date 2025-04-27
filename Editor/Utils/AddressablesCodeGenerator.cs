using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    internal static class AddressablesCodeGenerator
    {
        public static void GenerateCode(AddressableCatalog catalog, string path)
        {
            var b = new CodeBuilder();
            b.Using("UnityEngine.AddressableAssets");
            b.Blank();

            using (b.Public_Static_Class("Addresses"))
            {
                var addressList = catalog.GetAddressList();
                foreach (var addressName in addressList)
                {
                    var address = AddressUtils.Hash(addressName);
                    b.Public_Const_Field("Address", addressName, $"(Address) {(uint) address}u", comment: address.Hex());
                }
            }

            b.WriteTo(path);
        }
    }
}