using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// Keys for this location.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Dependency keys.
        /// </summary>
        public List<string> Dependencies { get; private set; }

        /// <summary>
        /// Serializable data for the provider.
        /// </summary>
        public object Data { get; set; }

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
        /// <param name="extraData">Optional additional data to be passed to the provider.  For example, AssetBundleProviders use this for cache and crc data.</param>
        public ContentCatalogDataEntry(Type type, string internalId, string key, IEnumerable<string> dependencies = null, object extraData = null)
        {
            InternalId = internalId;
            ResourceType = type;
            Key = key;
            Dependencies = dependencies == null ? new List<string>() : new List<string>(dependencies);
            Data = extraData;
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

        internal IResourceLocator CreateCustomLocator(int locatorCacheSize = 100)
        {
            return new ResourceLocator(m_Reader, locatorCacheSize);
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
                var keyToEntryIndices = new Dictionary<string, List<int>>();
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    var k = e.Key;
                    if (!keyToEntryIndices.TryGetValue(k, out var indices))
                        keyToEntryIndices.Add(k, indices = new List<int>());
                    indices.Add(i);
                }
                //reserve header and keys to ensure they are first
                var keysOffset = writer.Reserve<ResourceLocator.KeyData>((uint)keyToEntryIndices.Count);

                //create array of all locations
                var locationIds = new uint[entries.Count];
                for (int i = 0; i < entries.Count; i++)
                    locationIds[i] = writer.WriteObject(new ResourceLocator.ContentCatalogDataEntrySerializationContext { entry = entries[i], allEntries = entries, keyToEntryIndices = keyToEntryIndices }, false);


                //create array of all keys
                int keyIndex = 0;
                var allKeys = new ResourceLocator.KeyData[keyToEntryIndices.Count];
                foreach (var k in keyToEntryIndices)
                {
                    //create array of location ids
                    var locationOffsets = k.Value.Select(i => locationIds[i]).ToArray();

                    allKeys[keyIndex++] = new ResourceLocator.KeyData
                    {
                        keyNameOffset = writer.WriteObject(k.Key, true),
                        locationSetOffset = writer.Write(locationOffsets)
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
                public uint locationSetOffset;
            }

            internal class ContentCatalogDataEntrySerializationContext
            {
                public ContentCatalogDataEntry entry;
                public Dictionary<string, List<int>> keyToEntryIndices;
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
                        public uint extraDataOffset;
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
                        uint depId = uint.MaxValue;
                        if (e.Dependencies != null && e.Dependencies.Count > 0)
                        {
                            var depIds = new HashSet<uint>();
                            foreach (var k in e.Dependencies)
                                foreach (var i in ec.keyToEntryIndices[k])
                                    depIds.Add(writer.WriteObject(new ContentCatalogDataEntrySerializationContext { entry = ec.allEntries[i], allEntries = ec.allEntries, keyToEntryIndices = ec.keyToEntryIndices }, false));
                            depId = writer.Write(depIds.ToArray(), false);
                        }
                        var data = new Data
                        {
                            primaryKeyOffset = writer.WriteString(e.Key, '/'),
                            internalIdOffset = writer.WriteString(e.InternalId, '/'),
                            dependencySetOffset = depId,
                            extraDataOffset = writer.WriteObject(e.Data, true),
                            typeId = writer.WriteObject(e.ResourceType, false)
                        };
                        return writer.Write(data);
                    }
                }

                public ResourceLocation(BinaryStorageBuffer.Reader r, uint id)
                {
                    var d = r.ReadValue<Serializer.Data>(id);
                    PrimaryKey = r.ReadString(d.primaryKeyOffset, '/', false);
                    InternalId = Addressables.ResolveInternalId(r.ReadString(d.internalIdOffset, '/', false));
                    Data = r.ReadObject(d.extraDataOffset, false);
                    Dependencies = r.ReadObjectArray<ResourceLocation>(d.dependencySetOffset, true);
                    DependencyHashCode = (int)d.dependencySetOffset;
                    ResourceType = r.ReadObject<Type>(d.typeId);

                    ProviderId = ResourceType == typeof(IAssetBundleResource)
                        ? typeof(AssetBundleProvider).FullName : typeof(BundledAssetProvider).FullName;
                }

                public string PrimaryKey { private set; get; }
                public string InternalId { private set; get; }
                public object Data { private set; get; }
                public string ProviderId { set; get; }
                public IList<IResourceLocation> Dependencies { private set; get; }
                public int DependencyHashCode { private set; get; }
                public bool HasDependencies => DependencyHashCode >= 0;
                public Type ResourceType { private set; get; }
                public int Hash(Type t) => (InternalId.GetHashCode() * 31 + t.GetHashCode()) * 31 + DependencyHashCode;
            }

            struct CacheKey : IEquatable<CacheKey>
            {
                public object key;
                public Type type;
                int hashCode;
                public CacheKey(object o, Type t)
                {
                    key = o;
                    type = t;
                    hashCode = type == null ? key.GetHashCode() : key.GetHashCode() * 31 + type.GetHashCode();
                }
                public bool Equals(CacheKey other) => key.Equals(other.key) && ((type == null && other.type == null) || type.Equals(other.type));
                public override int GetHashCode() => hashCode;
            }

            LRUCache<CacheKey, IList<IResourceLocation>> m_Cache;
            Dictionary<object, uint> keyData;
            BinaryStorageBuffer.Reader reader;
            public IEnumerable<object> Keys => keyData.Keys;

            //TODO: this is VERY expensive with this locator since it will expand the entire thing into memory and then throw most of it away.
            public IEnumerable<IResourceLocation> AllLocations
            {
                get
                {
                    var allLocs = new HashSet<IResourceLocation>();
                    foreach (var kvp in keyData)
                    {
                        if (Locate(kvp.Key, null, out var locs))
                        {
                            foreach (var l in locs)
                                allLocs.Add(l);
                        }
                    }
                    return allLocs;
                }
            }

            internal ResourceLocator(BinaryStorageBuffer.Reader reader, int cacheLimit)
            {
                this.reader = reader;
                m_Cache = new LRUCache<CacheKey, IList<IResourceLocation>>(cacheLimit);
                keyData = new Dictionary<object, uint>();
                uint keysOffset = sizeof(uint);
                var keyDataArray = reader.ReadValueArray<KeyData>(keysOffset, false);
                int index = 0;
                foreach (var k in keyDataArray)
                {
                    var key = reader.ReadObject(k.keyNameOffset);
                    keyData.Add(key, k.locationSetOffset);
                    index++;
                }
            }

            public bool Locate(object key, Type type, out IList<IResourceLocation> locations)
            {
                var cacheKey = new CacheKey(key, type);
                if (m_Cache.TryGet(cacheKey, out locations))
                    return true;
                if (!keyData.TryGetValue(key, out var locationSetOffset))
                {
                    locations = null;
                    return false;
                }
                var locs = reader.ReadObjectArray<ResourceLocation>(locationSetOffset);
                if (type == null || type == typeof(object))
                {
                    locations = locs;
                    m_Cache.TryAdd(cacheKey, locations);
                    return true;
                }
                var validTypeCount = 0;
                foreach (var l in locs)
                    if (type.IsAssignableFrom(l.ResourceType))
                        validTypeCount++;

                if (validTypeCount == 0)
                {
                    locations = null;
                    m_Cache.TryAdd(cacheKey, locations);
                    return false;
                }

                if (validTypeCount == locs.Length)
                {
                    locations = locs;
                    m_Cache.TryAdd(cacheKey, locations);
                    return true;
                }

                locations = new List<IResourceLocation>();
                foreach (var l in locs)
                {
                    if (type.IsAssignableFrom(l.ResourceType))
                        locations.Add(l);
                }
                m_Cache.TryAdd(cacheKey, locations);
                locations = locs;
                return locations != null;
            }
        }
    }
}
