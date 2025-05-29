namespace Bundles.Editor
{
    internal static class AddressCodeGen
    {
        public static void GenerateCode(AssetCatalog catalog, string path)
        {
            var b = new CodeBuilder();
            b.Using("Bundles");
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