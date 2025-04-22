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
                foreach (var address in addressList)
                    b.Public_Const_Field("Address", address, $"(Address) {AddressUtils.Hash(address)}u");
            }

            b.WriteTo(path);
        }
    }
}