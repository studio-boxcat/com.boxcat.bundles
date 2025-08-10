using System.Collections.Generic;
using UnityEditor;

namespace Bundles.Editor
{
    internal readonly struct EntryDef
    {
        public readonly GUID GUID;
        public readonly Address? Address;
        public readonly AssetBundleId Bundle;
        public readonly HashSet<AssetBundleId> Dependencies;

        public EntryDef(GUID guid, Address? address, AssetBundleId bundle, HashSet<AssetBundleId> dependencies)
        {
            GUID = guid;
            Address = address;
            Bundle = bundle;
            Dependencies = dependencies;
        }

        public override string ToString()
        {
            if (Address is null)
            {
                var name = System.IO.Path.GetFileName(AssetDatabase.GUIDToAssetPath(GUID));
                return $"{name}  ({GUID}) > {Bundle}";
            }
            return $"{Address.Value.Name()} ({GUID}) > {Bundle}";
        }
    }
}