using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Assertions;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.AddressableAssets.ResourceLocators
{
    /// <summary>
    /// Contains serializable data for an IResourceLocation
    /// </summary>
    public class ContentCatalogDataEntry
    {
        /// <summary>
        /// Internl id.
        /// </summary>
        public string InternalId { get; set; }

        /// <summary>
        /// Key (Address) for this location. Null if the location does not have a explicit address.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Dependency keys.
        /// </summary>
        public List<string> Dependencies { get; private set; }

        /// <summary>
        /// The type of the resource for th location.
        /// </summary>
        public Type ResourceType { get; private set; }

        /// <summary>
        /// Creates a new ContentCatalogEntry object.
        /// </summary>
        /// <param name="type">The entry type.</param>
        /// <param name="internalId">The internal id.</param>
        /// <param name="key">The collection of keys that can be used to retrieve this entry.</param>
        /// <param name="dependencies">Optional collection of keys for dependencies.</param>
        public ContentCatalogDataEntry(Type type, string internalId, string key, IEnumerable<string> dependencies = null)
        {
            InternalId = internalId;
            ResourceType = type;
            Key = key;
            Dependencies = dependencies == null ? new List<string>() : new List<string>(dependencies);
        }
    }

    /// <summary>
    /// Container for ContentCatalogEntries.
    /// </summary>
    [Serializable]
    public class ContentCatalogData
    {
        [SerializeField]
        internal string m_BuildResultHash;

        IList<ContentCatalogDataEntry> m_Entries;

        /// <summary>
        /// Create a new ContentCatalogData object without any data.
        /// </summary>
        public ContentCatalogData()
        {
        }

#if UNITY_EDITOR
        /// <summary>
        /// Creates a new ContentCatalogData object with the specified locator id.
        /// </summary>
        /// <param name="id">The id of the locator.</param>
        public ContentCatalogData(IList<ContentCatalogDataEntry> entries)
        {
            SetData(entries);
        }
#endif


        internal void CleanData()
        {
            m_Reader = null;
        }


#if UNITY_EDITOR
        /// <summary>
        /// Construct catalog data with entries.
        /// </summary>
        /// <param name="data">The data entries.</param>
        /// <param name="id">The locator id.</param>
        public byte[] SerializeToByteArray()
        {
            var wr = new BinaryStorageBuffer.Writer(0, new Serializer());
            wr.WriteObject(this, false);
            return wr.SerializeToByteArray();
        }

        public void SetData(IList<ContentCatalogDataEntry> entries)
        {
            m_Entries = entries;
            m_Reader = new BinaryStorageBuffer.Reader(SerializeToByteArray(), 1024, new Serializer());
        }

        internal void SaveToFile(string path)
        {
            var bytes = SerializeToByteArray();
            File.WriteAllBytes(path, bytes);
        }
#endif

        BinaryStorageBuffer.Reader m_Reader;

        internal ContentCatalogData(BinaryStorageBuffer.Reader reader)
        {
            m_Reader = reader;
        }

        internal class Serializer : BinaryStorageBuffer.ISerializationAdapter<ContentCatalogData>
        {
            public IEnumerable<BinaryStorageBuffer.ISerializationAdapter> Dependencies => new BinaryStorageBuffer.ISerializationAdapter[]
            {
                new ResourceLocator.ResourceLocation.Serializer()
            };

            public object Deserialize(BinaryStorageBuffer.Reader reader, Type t, uint offset)
            {
                return new ContentCatalogData(reader);
            }

            public uint Serialize(BinaryStorageBuffer.Writer writer, object val)
            {
                var cd = val as ContentCatalogData;
                var entries = cd.m_Entries;
                var keyToEntryIndex = new Dictionary<string, int>();
                for (var i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    var k = e.Key;
                    keyToEntryIndex.Add(k, i);
                }
                //reserve header and keys to ensure they are first
                var keysOffset = writer.Reserve<ResourceLocator.KeyData>((uint)keyToEntryIndex.Count);

                //create array of all locations
                var locationIds = new uint[entries.Count];
                for (int i = 0; i < entries.Count; i++)
                {
                    locationIds[i] = writer.WriteObject(new ResourceLocator.ContentCatalogDataEntrySerializationContext
                    {
                        entry = entries[i],
                        allEntries = entries,
                        keyToEntryIndex = keyToEntryIndex
                    }, false);
                }


                //create array of all keys
                int keyIndex = 0;
                var allKeys = new ResourceLocator.KeyData[keyToEntryIndex.Count];
                foreach (var k in keyToEntryIndex)
                {
                    allKeys[keyIndex++] = new ResourceLocator.KeyData
                    {
                        keyNameOffset = writer.WriteObject(k.Key, true),
                        locationOffset = writer.Write(locationIds[k.Value])
                    };
                }
                writer.Write(keysOffset, allKeys);
                return keysOffset;
            }
        }
        
        internal class ResourceLocator : IResourceLocator
        {
            public struct KeyData
            {
                public uint keyNameOffset;
                public uint locationOffset;
            }

            internal class ContentCatalogDataEntrySerializationContext
            {
                public ContentCatalogDataEntry entry;
                public Dictionary<string, int> keyToEntryIndex;
                public IList<ContentCatalogDataEntry> allEntries;
            }

            internal class ResourceLocation : IResourceLocation
            {
                public class Serializer : BinaryStorageBuffer.ISerializationAdapter<ResourceLocation>, BinaryStorageBuffer.ISerializationAdapter<ContentCatalogDataEntrySerializationContext>
                {
                    public struct Data
                    {
                        public uint primaryKeyOffset;
                        public uint internalIdOffset;
                        public uint dependencySetOffset;
                        public int dependencyHashValue;
                        public uint typeId;
                    }

                    public IEnumerable<BinaryStorageBuffer.ISerializationAdapter> Dependencies => null;

                    //read as location
                    public object Deserialize(BinaryStorageBuffer.Reader reader, Type t, uint offset)
                    {
                        return new ResourceLocation(reader, offset);
                    }

                    //write from data entry
                    public uint Serialize(BinaryStorageBuffer.Writer writer, object val)
                    {
                        var ec = val as ContentCatalogDataEntrySerializationContext;

                        var e = ec.entry;
                        Assert.IsTrue(e.InternalId.Length < 260, "InternalId length is too long. On some platforms, long paths are not supported.");

                        uint depId = uint.MaxValue;
                        if (e.Dependencies != null && e.Dependencies.Count > 0)
                        {
                            var depIds = new HashSet<uint>();
                            foreach (var k in e.Dependencies)
                            {
                                var i = ec.keyToEntryIndex[k];
                                depIds.Add(writer.WriteObject(new ContentCatalogDataEntrySerializationContext
                                {
                                    entry = ec.allEntries[i],
                                    allEntries = ec.allEntries,
                                    keyToEntryIndex = ec.keyToEntryIndex
                                }, false));
                            }
                            depId = writer.Write(depIds.ToArray(), false);
                        }

                        var data = new Data
                        {
                            primaryKeyOffset = writer.WriteString(e.Key, '/'),
                            internalIdOffset = writer.WriteString(e.InternalId, '/'),
                            dependencySetOffset = depId,
                            typeId = writer.WriteObject(e.ResourceType, false)
                        };
                        return writer.Write(data);
                    }
                }

                public ResourceLocation(BinaryStorageBuffer.Reader r, uint id)
                {
                    var d = r.ReadValue<Serializer.Data>(id);
                    PrimaryKey = r.ReadString(d.primaryKeyOffset, '/', false);
                    InternalId = r.ReadString(d.internalIdOffset, '/', false);
                    Dependencies = r.ReadObjectArray<ResourceLocation>(d.dependencySetOffset, true);
                    DependencyHashCode = (int)d.dependencySetOffset;
                    ResourceType = r.ReadObject<Type>(d.typeId);

                    ProviderId = ResourceType == typeof(IAssetBundleResource)
                        ? ResourceProviderType.AssetBundle : ResourceProviderType.BundledAsset;
                }

                public string PrimaryKey { private set; get; }
                public string InternalId { private set; get; }
                public ResourceProviderType ProviderId { set; get; }
                public IList<IResourceLocation> Dependencies { private set; get; }
                public int DependencyHashCode { private set; get; }
                public bool HasDependencies => DependencyHashCode >= 0;
                public Type ResourceType { private set; get; }
                public int Hash(Type t) => (InternalId.GetHashCode() * 31 + t.GetHashCode()) * 31 + DependencyHashCode;
            }

            readonly Dictionary<string, uint> keyData;
            readonly BinaryStorageBuffer.Reader reader;

            internal ResourceLocator(BinaryStorageBuffer.Reader reader)
            {
                this.reader = reader;
                keyData = new Dictionary<string, uint>();
                var keyDataArray = reader.ReadValueArray<KeyData>(sizeof(uint), false);
                int index = 0;
                foreach (var k in keyDataArray)
                {
                    var key = (string) reader.ReadObject(k.keyNameOffset);
                    keyData.Add(key, k.locationOffset);
                    index++;
                }
            }

            public IResourceLocation Locate(string key, Type type)
            {
                Assert.IsNotNull(type);

                if (keyData.TryGetValue(key, out var locationOffset) is false)
                    throw new KeyNotFoundException($"Key not found: {key}");

                var loc = reader.ReadObject<ResourceLocation>(locationOffset);
                Assert.IsTrue(type.IsAssignableFrom(loc.ResourceType), $"ResourceType mismatch. Key: {key}, Expected: {type}, Actual: {loc.ResourceType}");
                return loc;
            }
        }
    }
}
