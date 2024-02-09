using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Assertions;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Serialization;
using UnityEngine.U2D;

namespace UnityEditor.AddressableAssets.Settings
{
    /// <summary>
    /// Contains data for an addressable asset entry.
    /// </summary>
    [Serializable]
    public class AddressableAssetEntry : ISerializationCallbackReceiver
    {
        [FormerlySerializedAs("m_guid")]
        [SerializeField]
        string m_GUID;

        [FormerlySerializedAs("m_address")]
        [SerializeField]
        string m_Address;

        [FormerlySerializedAs("m_readOnly")]
        [SerializeField]
        bool m_ReadOnly;

        internal virtual bool HasSettings()
        {
            return false;
        }

        /// <summary>
        /// List of AddressableAssetEntries that are considered sub-assets of a main Asset.  Typically used for Folder entires.
        /// </summary>
        [NonSerialized]
        public List<AddressableAssetEntry> SubAssets = new List<AddressableAssetEntry>();

        [NonSerialized]
        AddressableAssetGroup m_ParentGroup;

        /// <summary>
        /// The asset group that this entry belongs to.  An entry can only belong to a single group at a time.
        /// </summary>
        public AddressableAssetGroup parentGroup
        {
            get { return m_ParentGroup; }
            set { m_ParentGroup = value; }
        }

        /// <summary>
        /// The id for the bundle file.
        /// </summary>
        public string BundleFileId { get; set; }

        /// <summary>
        /// The asset guid.
        /// </summary>
        public string guid
        {
            get { return m_GUID; }
        }

        /// <summary>
        /// The address of the entry.  This is treated as the primary key in the ResourceManager system.
        /// </summary>
        public string address
        {
            get { return m_Address; }
            set { SetAddress(value); }
        }

        /// <summary>
        /// Set the address of the entry.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <param name="postEvent">Post modification event.</param>
        public void SetAddress(string addr, bool postEvent = true)
        {
            if (m_Address != addr)
            {
                m_Address = addr;
                if (string.IsNullOrEmpty(m_Address))
                    m_Address = AssetPath;
                if (m_GUID.Length > 0 && m_Address.Contains('[') && m_Address.Contains(']'))
                    Debug.LogErrorFormat("Address '{0}' cannot contain '[ ]'.", m_Address);
                SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, this, postEvent);
            }
        }

        /// <summary>
        /// Read only state of the entry.
        /// </summary>
        public bool ReadOnly
        {
            get { return m_ReadOnly; }
            set
            {
                if (m_ReadOnly != value)
                {
                    m_ReadOnly = value;
                    SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, this, true);
                }
            }
        }

        /// <summary>
        /// Is a sub asset.  For example an asset in an addressable folder.
        /// </summary>
        public bool IsSubAsset { get; set; }

        /// <summary>
        /// Stores a reference to the parent entry. Only used if the asset is a sub asset.
        /// </summary>
        public AddressableAssetEntry ParentEntry { get; set; }

        bool m_CheckedIsScene;
        bool m_IsScene;

        /// <summary>
        /// Is this entry for a scene.
        /// </summary>
        public bool IsScene
        {
            get
            {
                if (!m_CheckedIsScene)
                {
                    m_CheckedIsScene = true;
                    m_IsScene = AssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
                }

                return m_IsScene;
            }
        }

        internal Type m_cachedMainAssetType = null;

        /// <summary>
        /// The System.Type of the main Object referenced by an AddressableAssetEntry
        /// </summary>
        public Type MainAssetType
        {
            get
            {
                if (m_cachedMainAssetType == null)
                {
                    m_cachedMainAssetType = AssetDatabase.GetMainAssetTypeAtPath(AssetPath);
                    if (m_cachedMainAssetType == null)
                        return typeof(object); // do not cache a bad type lookup.
                }

                return m_cachedMainAssetType;
            }
        }

        internal AddressableAssetEntry(string guid, string address, AddressableAssetGroup parent, bool readOnly)
        {
            if (guid.Length > 0 && address.Contains('[') && address.Contains(']'))
                Debug.LogErrorFormat("Address '{0}' cannot contain '[ ]'.", address);
            m_GUID = guid;
            m_Address = address;
            m_ReadOnly = readOnly;
            parentGroup = parent;
        }

        Hash128 m_CurrentHash;
        internal Hash128 currentHash
        {
            get
            {
                if (!m_CurrentHash.isValid)
                {
                    m_CurrentHash.Append(m_GUID);
                    m_CurrentHash.Append(m_Address);
                    var flags = (m_ReadOnly ? 1 : 0) | (IsSubAsset ? 8 : 0);
                    m_CurrentHash.Append(flags);
                }
                return m_CurrentHash;
            }
        }

        internal void SetDirty(AddressableAssetSettings.ModificationEvent e, object o, bool postEvent)
        {
            m_CurrentHash = default;
            if (parentGroup != null)
                parentGroup.SetDirty(e, o, postEvent, true);
        }

        internal void SetCachedPath(string newCachedPath)
        {
            if (newCachedPath != m_cachedAssetPath)
            {
                m_cachedAssetPath = newCachedPath;
                m_MainAsset = null;
                m_TargetAsset = null;
            }
        }

        internal void SetSubObjectType(Type type)
        {
            m_cachedMainAssetType = type;
        }

        internal string m_cachedAssetPath = null;

        /// <summary>
        /// The path of the asset.
        /// </summary>
        public string AssetPath
        {
            get
            {
                if (string.IsNullOrEmpty(m_cachedAssetPath))
                {
                    if (string.IsNullOrEmpty(guid))
                        SetCachedPath(string.Empty);
                    else
                        SetCachedPath(AssetDatabase.GUIDToAssetPath(guid));
                }

                return m_cachedAssetPath;
            }
        }

        private UnityEngine.Object m_MainAsset;

        /// <summary>
        /// The main asset object for this entry.
        /// </summary>
        public UnityEngine.Object MainAsset
        {
            get
            {
                if (m_MainAsset == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m_MainAsset, out string guid, out long localId))
                {
                    AddressableAssetEntry e = this;
                    while (string.IsNullOrEmpty(e.AssetPath))
                    {
                        if (e.ParentEntry == null)
                            return null;
                        e = e.ParentEntry;
                    }

                    m_MainAsset = AssetDatabase.LoadMainAssetAtPath(e.AssetPath);
                }

                return m_MainAsset;
            }
        }

        private UnityEngine.Object m_TargetAsset;

        /// <summary>
        /// The asset object for this entry.
        /// </summary>
        public UnityEngine.Object TargetAsset
        {
            get
            {
                if (m_TargetAsset == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m_TargetAsset, out string guid, out long localId))
                {
                    if (!string.IsNullOrEmpty(AssetPath) || !IsSubAsset)
                    {
                        m_TargetAsset = MainAsset;
                        return m_TargetAsset;
                    }

                    if (ParentEntry == null || !string.IsNullOrEmpty(AssetPath) || string.IsNullOrEmpty(ParentEntry.AssetPath))
                        return null;

                    var mainAsset = ParentEntry.MainAsset;
                    if (ResourceManagerConfig.ExtractKeyAndSubKey(address, out string mainKey, out string subObjectName))
                    {
                        if (mainAsset != null && mainAsset.GetType() == typeof(SpriteAtlas))
                        {
                            m_TargetAsset = (mainAsset as SpriteAtlas).GetSprite(subObjectName);
                            return m_TargetAsset;
                        }

                        var subObjects = AssetDatabase.LoadAllAssetRepresentationsAtPath(ParentEntry.AssetPath);
                        foreach (var s in subObjects)
                        {
                            if (s != null && s.name == subObjectName)
                            {
                                m_TargetAsset = s;
                                break;
                            }
                        }
                    }
                }

                return m_TargetAsset;
            }
        }

        /// <summary>
        /// The asset load path.  This is used to determine the internal id of resource locations.
        /// </summary>
        /// <param name="otherLoadPaths">The internal ids of the asset, typically shortened versions of the asset's GUID.</param>
        /// <returns>Return the runtime path that should be used to load this entry.</returns>
        public string GetAssetLoadPath(HashSet<string> otherLoadPaths)
        {
            return GetShortestUniqueString(guid, otherLoadPaths);

            static string GetShortestUniqueString(string guid, HashSet<string> otherLoadPaths)
            {
                var g = guid;
                if (otherLoadPaths == null)
                    return g;
                var len = 1;
                var p = g.Substring(0, len);
                while (otherLoadPaths.Contains(p))
                    p = g.Substring(0, ++len);
                otherLoadPaths.Add(p);
                return p;
            }
        }

        static string GetResourcesPath(string path)
        {
            path = path.Replace('\\', '/');
            int ri = path.LastIndexOf("/resources/", StringComparison.OrdinalIgnoreCase);
            if (ri >= 0)
                path = path.Substring(ri + "/resources/".Length);
            int i = path.LastIndexOf('.');
            if (i > 0)
                path = path.Substring(0, i);
            return path;
        }

        /// <summary>
        /// Gets an entry for this folder entry
        /// </summary>
        /// <param name="subAssetGuid"></param>
        /// <param name="subAssetPath"></param>
        /// <returns></returns>
        /// <remarks>Assumes that this asset entry is a valid folder asset</remarks>
        internal AddressableAssetEntry GetFolderSubEntry(string subAssetGuid, string subAssetPath)
        {
            string assetPath = AssetPath;
            if (string.IsNullOrEmpty(assetPath) || !subAssetPath.StartsWith(assetPath, StringComparison.Ordinal))
                return null;
            var settings = parentGroup.Settings;

            AddressableAssetEntry assetEntry = settings.FindAssetEntry(subAssetGuid);
            if (assetEntry != null)
            {
                if (assetEntry.IsSubAsset && assetEntry.ParentEntry == this)
                    return assetEntry;
                return null;
            }

            string relativePath = subAssetPath.Remove(0, assetPath.Length + 1);
            string[] splitRelativePath = relativePath.Split('/');
            string folderPath = assetPath;
            for (int i = 0; i < splitRelativePath.Length - 1; ++i)
            {
                folderPath = folderPath + "/" + splitRelativePath[i];
                string folderGuid = AssetDatabase.AssetPathToGUID(folderPath);
                if (!AddressableAssetUtility.IsPathValidForEntry(folderPath))
                    return null;
                var folderEntry = settings.CreateSubEntryIfUnique(folderGuid, address + "/" + folderPath.Remove(assetPath.Length), this);
                if (folderEntry != null)
                {
                    throw new NotSupportedException(folderPath);
                }
                else
                    return null;
            }

            assetEntry = settings.CreateSubEntryIfUnique(subAssetGuid, address + "/" + relativePath, this);

            if (assetEntry == null || assetEntry.IsSubAsset == false)
                return null;
            return assetEntry;
        }

        static IEnumerable<string> GetResourceDirectories()
        {
            string[] resourcesGuids = AssetDatabase.FindAssets("Resources", new string[] {"Assets", "Packages"});
            foreach (string resourcesGuid in resourcesGuids)
            {
                string resourcesAssetPath = AssetDatabase.GUIDToAssetPath(resourcesGuid);
                if (resourcesAssetPath.EndsWith("/resources", StringComparison.OrdinalIgnoreCase) && AssetDatabase.IsValidFolder(resourcesAssetPath) && Directory.Exists(resourcesAssetPath))
                {
                    yield return resourcesAssetPath;
                }
            }
        }

        internal AddressableAssetEntry GetImplicitEntry(string implicitAssetGuid, string implicitAssetPath)
        {
            return GetFolderSubEntry(implicitAssetGuid, implicitAssetPath);
        }

        string GetRelativePath(string file, string path)
        {
            return file.Substring(path.Length);
        }

        public void OnBeforeSerialize()
        {
        }

        /// <summary>
        /// Implementation of ISerializationCallbackReceiver.  Converts data from serializable form after deserialization.
        /// </summary>
        public void OnAfterDeserialize()
        {
            m_cachedMainAssetType = null;
            m_cachedAssetPath = null;
            m_CheckedIsScene = false;
            m_MainAsset = null;
            m_TargetAsset = null;
            SubAssets?.Clear();
        }

        /// <summary>
        /// Returns the address of the AddressableAssetEntry.
        /// </summary>
        /// <returns>The address of the AddressableAssetEntry</returns>
        public override string ToString()
        {
            return m_Address;
        }

        /// <summary>
        /// Create all entries for this addressable asset.  This will expand subassets (Sprites, Meshes, etc) and also different representations.
        /// </summary>
        /// <param name="dependencies">Keys of dependencies</param>
        /// <param name="depInfo">Map of guids to AssetLoadInfo for object identifiers in an Asset.  If null, ContentBuildInterface gathers object ids automatically.</param>
        /// <param name="assetsInBundle">The internal ids of the asset, typically shortened versions of the asset's GUID.</param>
        public ContentCatalogDataEntry CreateCatalogEntry(IEnumerable<string> dependencies, Dictionary<GUID, AssetLoadInfo> depInfo, HashSet<string> assetsInBundle)
        {
            Assert.IsFalse(string.IsNullOrEmpty(address), "Address is null or empty");
            Assert.IsFalse(string.IsNullOrEmpty(AssetPath), "AssetPath is null or empty");
            Assert.IsNotNull(depInfo, "depInfo is null");

            L.I($"[AddressableAssetEntry] Creating catalog entry for {address}");
            var mainType = ResolveRepresentingType(MainAssetType, guid, depInfo);
            var assetPath = GetAssetLoadPath(assetsInBundle);
            L.I($"[AddressableAssetEntry] CreateCatalogEntry: {address}, {mainType}, {assetPath}, [{string.Join(",", dependencies)}]");
            return new ContentCatalogDataEntry(mainType, assetPath, address, dependencies);
        }

        static Type ResolveRepresentingType(Type type, string guid, Dictionary<GUID, AssetLoadInfo> depInfo)
        {
            var mainType = AddressableAssetUtility.MapEditorTypeToRuntimeType(type);
            Assert.IsNotNull(mainType, "Main type is null");
            Assert.AreNotEqual(typeof(DefaultAsset), mainType, "Main type is DefaultAsset");

            // When the main type is SceneInstance...
            if (mainType == typeof(SceneInstance))
                return mainType;

            // For the Texture imported as single Sprite, use Sprite type.
            if (mainType == typeof(Texture2D))
            {
                var includedObjs = depInfo[new GUID(guid)].includedObjects;
                if (includedObjs.Count == 2)
                {
                    Assert.AreEqual(mainType, ContentBuildInterface.GetTypeForObject(includedObjs[0]), "Main type is not Texture2D");
                    var objType = ContentBuildInterface.GetTypeForObject(includedObjs[1]);
                    if (objType == typeof(Sprite))
                        return typeof(Sprite);
                }
            }

            Assert.AreNotEqual(typeof(DefaultAsset), mainType, "Main type is DefaultAsset");
            return mainType;
        }

        public static Type ResolveRepresentingType(ObjectIdentifier[] ids)
        {
            // XXX: Assume that the first identifier is the main asset.
            var mainType = ContentBuildInterface.GetTypeForObject(ids[0]);

            if (ids.Length == 2 && mainType == typeof(Texture2D))
            {
                var objType = ContentBuildInterface.GetTypeForObject(ids[1]);
                if (objType == typeof(Sprite))
                    return typeof(Sprite);
            }

            return AddressableAssetUtility.MapEditorTypeToRuntimeType(mainType);
        }
    }
}