using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets.Build.Layout
{
    [Serializable]
    public struct AssetId : IEquatable<AssetId>, IComparable<AssetId>
    {
        public string Value;
        public bool IsPath;

        public AssetId(AssetGUID guid)
        {
            Value = guid.Value;
            IsPath = false;
        }

        public AssetId(GUID guid)
        {
            Value = guid.ToString();
            IsPath = false;
        }

        private AssetId(string path)
        {
            Value = path;
            IsPath = true;
        }

        public static AssetId FromPath(string path) => new AssetId(path);

        public override string ToString() => Value;

        public static implicit operator AssetId(AssetGUID guid) => new(guid);
        public static implicit operator AssetId(GUID guid) => new(guid);
        public static explicit operator GUID(AssetId id)
        {
            Assert.IsFalse(id.IsPath, "Cannot convert AssetId to GUID when IsPath is true");
            return new GUID(id.Value);
        }

        public bool Equals(AssetId other) => Value == other.Value && IsPath == other.IsPath;
        public override bool Equals(object obj) => obj is AssetId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Value, IsPath);

        public static bool operator ==(AssetId x, AssetId y) => x.Equals(y);
        public static bool operator !=(AssetId x, AssetId y) => !x.Equals(y);

        public int CompareTo(AssetId other)
        {
            var valueComparison = string.Compare(Value, other.Value, StringComparison.Ordinal);
            return valueComparison != 0 ? valueComparison : IsPath.CompareTo(other.IsPath);
        }
    }

    /// <summary>
    /// A storage class used to gather data about an Addressable build.
    /// </summary>
    [Serializable]
    internal class BuildLayout
    {
        /// <summary>
        /// Helper class to wrap header values for BuildLayout
        /// </summary>
        public class LayoutHeader
        {
            /// <summary>
            /// Build layout for this header
            /// </summary>
            internal BuildLayout m_BuildLayout;

            /// <summary>
            /// DateTime at the start of building Addressables
            /// </summary>
            public DateTime BuildStart
            {
                get
                {
                    if (m_BuildLayout == null)
                        return DateTime.MinValue;
                    return m_BuildLayout.BuildStart;
                }
            }

            /// <summary>
            /// Null or Empty if the build completed successfully, else contains error causing the failure
            /// </summary>
            public string BuildError
            {
                get
                {
                    if (m_BuildLayout == null)
                        return "";
                    return m_BuildLayout.BuildError;
                }
            }
        }

        /// <summary>
        /// Helper object to get header values for this build layout
        /// </summary>
        public LayoutHeader Header
        {
            get
            {
                if (m_Header == null)
                    m_Header = new LayoutHeader() { m_BuildLayout = this };
                return m_Header;
            }
        }

        private LayoutHeader m_Header;

        #region HeaderValues // Any values in here should also be in BuildLayoutHeader class

        /// <summary>
        /// Build Platform Addressables build is targeting
        /// </summary>
        public BuildTarget BuildTarget;

        /// <summary>
        /// DateTime at the start of building Addressables
        /// </summary>
        public DateTime BuildStart
        {
            get
            {
                if (m_BuildStartDateTime.Year > 2000)
                    return m_BuildStartDateTime;
                if (DateTime.TryParse(BuildStartTime, out DateTime result))
                {
                    m_BuildStartDateTime = result;
                    return m_BuildStartDateTime;
                }
                return DateTime.MinValue;
            }
            set
            {
                BuildStartTime = value.ToString();
            }
        }
        private DateTime m_BuildStartDateTime;

        [SerializeField]
        internal string BuildStartTime;

        /// <summary>
        /// Time in seconds taken to build Addressables Content
        /// </summary>
        public double Duration;

        /// <summary>
        /// Null or Empty if the build completed successfully, else contains error causing the failure
        /// </summary>
        public string BuildError;

        #endregion // End of header values

        /// <summary>
        /// Version of the Unity edtior used to perform the build.
        /// </summary>
        public string UnityVersion;

        /// <summary>
        /// The Addressable Groups that reference this data
        /// </summary>
        [SerializeReference]
        public List<Group> Groups = new List<Group>();

        /// <summary>
        /// The List of AssetBundles that were built without a group associated to them, such as the BuiltIn Shaders Bundle and the MonoScript Bundle
        /// </summary>
        [SerializeReference]
        public List<Bundle> BuiltInBundles = new List<Bundle>();

        /// <summary>
        /// List of assets with implicitly included Objects
        /// </summary>
        public List<AssetDuplicationData> DuplicatedAssets = new List<AssetDuplicationData>();

        internal string m_FilePath;

        private bool m_HeaderRead = false;
        private bool m_BodyRead = false;

        private FileStream m_FileStream = null;
        private StreamReader m_StreamReader = null;

        /// <summary>
        /// Used for serialising the header info for the BuildLayout.
        /// Names must match values in BuildLayout class
        /// </summary>
        [Serializable]
        private class BuildLayoutHeader
        {
            public BuildTarget BuildTarget;
            public string BuildStartTime;
            public double Duration;
            public string BuildError;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="path">Path to the BuildLayout json file on disk</param>
        /// <param name="readHeader">If the basic header information should be read</param>
        /// <param name="readFullFile">If the full build layout should be read</param>
        /// <returns></returns>
        public static BuildLayout Open(string path, bool readHeader = true, bool readFullFile = false)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                Debug.LogError($"Invalid path provided : {path}");
                return null;
            }

            BuildLayout readLayout = new BuildLayout
            {
                m_FilePath = path
            };

            if (readFullFile)
                readLayout.ReadFull();
            else if (readHeader)
                readLayout.ReadHeader();

            return readLayout;
        }

        /// <summary>
        /// Writes json file for the build layout to the destination path
        /// </summary>
        /// <param name="destinationPath">File path to write build layout</param>
        /// <param name="prettyPrint">If json should be written using pretty print</param>
        public void WriteToFile(string destinationPath, bool prettyPrint)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

            string versionElementString = "\"UnityVersion\":";
            string headerJson = null;
            string bodyJson = JsonUtility.ToJson(this, prettyPrint);

            if (prettyPrint)
            {
                BuildLayoutHeader header = new BuildLayoutHeader()
                {
                    BuildTarget = this.BuildTarget,
                    BuildStartTime = this.BuildStartTime,
                    Duration = this.Duration,
                    BuildError = this.BuildError
                };
                headerJson = JsonUtility.ToJson(header, false);
                headerJson = headerJson.Remove(headerJson.Length - 1, 1) + ',';
            }

            int index = bodyJson.IndexOf(versionElementString);
            if (prettyPrint)
                bodyJson = bodyJson.Remove(0, index);
            else
                bodyJson = bodyJson.Insert(index, "\n");

            using (FileStream s = System.IO.File.Open(destinationPath, FileMode.Create))
            {
                using (StreamWriter sw = new StreamWriter(s))
                {
                    if (prettyPrint)
                        sw.WriteLine(headerJson);
                    sw.Write(bodyJson);
                }
            }
        }

        /// <summary>
        /// Closes streams for loading the build layout
        /// </summary>
        public void Close()
        {
            if (m_StreamReader != null)
            {
                m_StreamReader.Close();
                m_StreamReader = null;
            }

            if (m_FileStream != null)
            {
                m_FileStream.Close();
                m_FileStream = null;
            }
        }

        /// <summary>
        /// Reads basic information about the build layout
        /// </summary>
        /// <param name="keepFileStreamsActive">If false, the file will be closed after reading the header line.</param>
        /// <returns>true is successful, else false</returns>
        public bool ReadHeader(bool keepFileStreamsActive = false)
        {
            if (m_HeaderRead)
                return true;

            if (string.IsNullOrEmpty(m_FilePath))
            {
                Debug.LogError("Cannot read BuildLayout header, A file has not been selected to open. Open must be called before reading any data");
                return false;
            }

            try
            {
                if (m_FileStream == null)
                {
                    m_FileStream = System.IO.File.Open(m_FilePath, FileMode.Open);
                    m_StreamReader = new StreamReader(m_FileStream);
                }

                string fileJsonText = m_StreamReader.ReadLine();
                int lastComma = fileJsonText.LastIndexOf(',');
                if (lastComma > 0)
                {
                    fileJsonText = fileJsonText.Remove(lastComma) + '}';
                    try
                    {
                        EditorJsonUtility.FromJsonOverwrite(fileJsonText, this);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to read header for BuildLayout at {m_FilePath}, with exception: {e.Message}");
                        return false;
                    }
                }
                else
                {
                    Debug.LogError($"Failed to read header for BuildLayout at {m_FilePath}, invalid json format");
                    return false;
                }

                m_HeaderRead = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
            finally
            {
                if (!keepFileStreamsActive)
                    Close();
            }

            return true;
        }

        /// <summary>
        /// Reads the full build layout data from file
        /// </summary>
        /// <returns>true is successful, else false</returns>
        public bool ReadFull()
        {
            if (m_BodyRead)
                return true;

            if (string.IsNullOrEmpty(m_FilePath))
            {
                Debug.LogError("Cannot read BuildLayout header, BuildLayout has not open for a file");
                return false;
            }

            try
            {
                if (m_FileStream == null)
                {
                    m_FileStream = System.IO.File.Open(m_FilePath, FileMode.Open);
                    m_StreamReader = new StreamReader(m_FileStream);
                }
                else if (m_HeaderRead)
                {
                    // reset to read the whole file
                    m_FileStream.Position = 0;
                    m_StreamReader.DiscardBufferedData();
                }

                string fileJsonText = m_StreamReader.ReadToEnd();
                EditorJsonUtility.FromJsonOverwrite(fileJsonText, this);
                m_HeaderRead = true;
                m_BodyRead = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to read header for BuildLayout at {m_FilePath}, with exception: {e.Message}");
                return false;
            }
            finally
            {
                Close();
            }

            return true;
        }

        /// <summary>
        /// Values set for the AddressablesAssetSettings at the time of building
        /// </summary>
        [Serializable]
        public class AddressablesEditorData
        {
            /// <summary>
            /// Hash value of the catalog at the time of building
            /// </summary>
            public string CatalogHash;

            /// <summary>
            /// Addressables setting value set for if the build used non recursive dependency calculation
            /// </summary>
            public bool NonRecursiveBuilding;

            /// <summary>
            /// Addressables setting value set for if the build used contiguous bundle objects
            /// </summary>
            public bool ContiguousBundles;
        }

        /// <summary>
        /// Information about the AssetBundleObject
        /// </summary>
        [Serializable]
        public class AssetBundleObjectInfo
        {
            /// <summary>
            /// The size, in bytes, of the AssetBundleObject
            /// </summary>
            public ulong Size;
        }

        /// <summary>
        /// Key value pair of string type
        /// </summary>
        [Serializable]
        public struct StringPair
        {
            /// <summary>
            /// String key
            /// </summary>
            public string Key;

            /// <summary>
            /// String value
            /// </summary>
            public string Value;
        }

        /// <summary>
        /// Data about the AddressableAssetGroup that gets processed during a build.
        /// </summary>
        [Serializable]
        public class Group
        {
            /// <summary>
            /// The Name of the AdressableAssetGroup
            /// </summary>
            public string Name;

            /// <summary>
            /// An AssetBundles associated with the Group
            /// </summary>
            [SerializeReference]
            public Bundle Bundle;
        }

        /// <summary>
        /// Data store for AssetBundle information.
        /// </summary>
        [Serializable]
        public class Bundle
        {
            public Bundle(BundleKey key, AssetBundleId id)
            {
                Key = key;
                Id = id;
            }

            /// <summary>
            /// The name of the AssetBundle
            /// </summary>
            public BundleKey Key;

            public AssetBundleId Id;

            public string Name => Key.ToString();

            /// <summary>
            /// The file size of the AssetBundle on disk, in bytes
            /// </summary>
            public ulong FileSize;

            /// <summary>
            /// The file size of all of the Expanded Dependencies of this AssetBundle, in bytes
            /// Expanded dependencies are the dependencies of this AssetBundle's dependencies
            /// </summary>
            public ulong ExpandedDependencyFileSize;

            /// <summary>
            /// The file size
            /// </summary>
            public ulong DependencyFileSize;

            /// <summary>
            /// The file size of the AssetBundle on disk when uncompressed, in bytes
            /// </summary>
            public ulong UncompressedFileSize
            {
                get
                {
                    ulong total = 0;
                    foreach (File file in Files)
                        total += file.UncompressedSize;
                    return total;
                }
            }

            /// <summary>
            /// The number of Assets contained within the bundle
            /// </summary>
            public int AssetCount = 0;

            /// <summary>
            /// Represents a dependency from the containing Bundle to dependentBundle, with AssetDependencies representing each of the assets in parentBundle that create the link to dependentBundle
            /// </summary>
            [Serializable]
            internal class BundleDependency
            {
                /// <summary>
                /// The bundle that the parent bundle depends on
                /// </summary>
                [SerializeReference]
                public Bundle DependencyBundle;

                /// <summary>
                /// The list of assets that link the parent bundle to the DependencyBundle
                /// </summary>
                public List<AssetDependency> AssetDependencies;

                /// <summary>
                /// Percentage of Efficiency asset usage that uses the entire dependency tree of this bundle dependency.
                /// This includes DependencyBundle and all bundles beneath it.
                /// Value is equal to [Total Filesize of Dependency Assets] / [Total size of all dependency bundles on disk]
                /// Example: There are 3 bundles A, B, and C, that are each 10 MB on disk. A depends on 2 MB worth of assets in B, and B depends on 4 MB worth of assets in C.
                /// The Efficiency of the dependencyLink from A->B would be 2/10 -> 20% and the ExpandedEfficiency of A->B would be (2 + 4)/(10 + 10) -> 6/20 -> 30%
                ///  </summary>
                public float ExpandedEfficiency;

                /// <summary>
                /// The Efficiency of the connection between the parent bundle and DependencyBundle irrespective of the full dependency tree below DependencyBundle.
                /// Value is equal to [Serialized Filesize of assets In Dependency Bundle Referenced By Parent]/[Total size of Dependency Bundle on disk]
                /// Example: Given two Bundles A and B that are each 10 MB on disk, and A depends on 5 MB worth of assets in B, then the Efficiency of DependencyLink A->B is 5/10 = .5
                /// </summary>
                public float Efficiency;

                private HashSet<ExplicitAsset> referencedAssets = new HashSet<ExplicitAsset>();

                /// <summary>
                /// The number of uniquely assets that the parent bundle uniquely references in dependency bundle. This is used to calculate Efficiency without double counting.
                /// </summary>
                internal ulong referencedAssetsFileSize = 0;

                internal BundleDependency(Bundle b)
                {
                    DependencyBundle = b;
                    AssetDependencies = new List<AssetDependency>();
                }

                internal void CreateAssetDependency(ExplicitAsset root, ExplicitAsset dependencyAsset)
                {
                    if (referencedAssets.Contains(dependencyAsset))
                        return;
                    referencedAssets.Add(dependencyAsset);
                    AssetDependencies.Add(new AssetDependency(root, dependencyAsset));
                    referencedAssetsFileSize += dependencyAsset.SerializedSize;
                }


                /// <summary>
                /// Represents a dependency from a root Asset to a dependent Asset.
                /// </summary>
                [Serializable]
                public struct AssetDependency
                {
                    [SerializeReference]
                    internal ExplicitAsset rootAsset;

                    [SerializeReference]
                    internal ExplicitAsset dependencyAsset;

                    internal AssetDependency(ExplicitAsset root, ExplicitAsset depAsset)
                    {
                        rootAsset = root;
                        dependencyAsset = depAsset;
                    }
                }
            }

            internal Dictionary<Bundle, BundleDependency> BundleDependencyMap = new();

            /// <summary>
            /// A list of bundles that this bundle depends upon.
            /// </summary>
            [SerializeField]
            public BundleDependency[] BundleDependencies = Array.Empty<BundleDependency>();


            /// <summary>
            /// Convert BundleDependencyMap to a format that is able to be serialized and plays nicer with
            /// CalculateEfficiency - this must be called on a bundle before CalculateEfficiency can be called.
            /// </summary>
            internal void SerializeBundleToBundleDependency()
            {
                BundleDependencies = new BundleDependency[BundleDependencyMap.Values.Count];
                BundleDependencyMap.Values.CopyTo(BundleDependencies, 0);
            }

            /// <summary>
            /// Updates the BundleDependency from the current bundle to the bundle that contains referencedAsset. If no such BundleDependency exists,
            /// one is created. Does nothing if rootAsset's bundle is not the current bundle or
            /// if the two assets are in the same bundle.
            /// </summary>
            /// <param name="rootAsset"></param>
            /// <param name="referencedAsset"></param>
            internal void UpdateBundleDependency(ExplicitAsset rootAsset, ExplicitAsset referencedAsset)
            {
                if (rootAsset.Bundle != this || referencedAsset.Bundle == rootAsset.Bundle)
                    return;

                if (!BundleDependencyMap.ContainsKey(referencedAsset.Bundle))
                    BundleDependencyMap.Add(referencedAsset.Bundle, new BundleDependency(referencedAsset.Bundle));
                BundleDependencyMap[referencedAsset.Bundle].CreateAssetDependency(rootAsset, referencedAsset);
            }

            // Helper struct for calculating Efficiency
            internal struct EfficiencyInfo
            {
                internal ulong totalAssetFileSize;
                internal ulong referencedAssetFileSize;
            }


            /// <summary>
            /// A reference to the Group data that this AssetBundle was generated from
            /// </summary>
            [SerializeReference]
            public Group Group;

            public string LoadPath => Key.GetBuildName();

            /// <summary>
            /// List of the Files referenced by the AssetBundle
            /// </summary>
            [SerializeReference]
            public List<File> Files = new List<File>();

            /// <summary>
            /// A list of the bundles that directly depend on this AssetBundle
            /// </summary>
            [SerializeReference]
            public List<Bundle> DependentBundles = new List<Bundle>();

            /// <summary>
            /// A list of the direct dependencies of the AssetBundle
            /// </summary>
            [SerializeReference]
            public List<Bundle> Dependencies;

            /// <summary>
            /// The second order dependencies and greater of a bundle
            /// </summary>
            [SerializeReference]
            public List<Bundle> ExpandedDependencies;
        }

        /// <summary>
        /// Data store for resource files generated by the build pipeline and referenced by a main File
        /// </summary>
        [Serializable]
        public class SubFile
        {
            /// <summary>
            /// The name of the sub-file
            /// </summary>
            public string Name;

            /// <summary>
            /// If the main File is a serialized file, this will be true.
            /// </summary>
            public bool IsSerializedFile;

            /// <summary>
            /// The size of the sub-file, in bytes
            /// </summary>
            public ulong Size;
        }

        /// <summary>
        /// Data store for the main File created for the AssetBundle
        /// </summary>
        [Serializable]
        internal class File
        {
            /// <summary>
            /// The name of the File.
            /// </summary>
            public string Name;

            /// <summary>
            /// The AssetBundle data that relates to a built file.
            /// </summary>
            [SerializeReference]
            public Bundle Bundle;

            /// <summary>
            /// The file size of the AssetBundle on disk when uncompressed, in bytes
            /// </summary>
            public ulong UncompressedSize
            {
                get
                {
                    ulong total = 0;
                    foreach (SubFile subFile in SubFiles)
                        total += subFile.Size;
                    return total;
                }
            }

            /// <summary>
            /// List of the resource files created by the build pipeline that a File references
            /// </summary>
            [SerializeReference]
            public List<SubFile> SubFiles = new List<SubFile>();

            /// <summary>
            /// A list of the explicit asset defined in the AssetBundle
            /// </summary>
            [SerializeReference]
            public List<ExplicitAsset> Assets = new List<ExplicitAsset>();

            /// <summary>
            /// A list of implicit assets built into the AssetBundle, typically through references by Assets that are explicitly defined.
            /// </summary>
            [SerializeReference]
            public List<DataFromOtherAsset> OtherAssets = new List<DataFromOtherAsset>();

            [SerializeReference]
            internal List<ExplicitAsset> ExternalReferences = new List<ExplicitAsset>();

            /// <summary>
            /// The final filename of the AssetBundle file
            /// </summary>
            public string WriteResultFilename;

            /// <summary>
            /// Data about the AssetBundleObject
            /// </summary>
            public AssetBundleObjectInfo BundleObjectInfo;

            /// <summary>
            /// The size of the data that needs to be preloaded for this File.
            /// </summary>
            public int PreloadInfoSize;

            /// <summary>
            /// The number of Mono scripts referenced by the File
            /// </summary>
            public int MonoScriptCount;

            /// <summary>
            /// The size of the Mono scripts referenced by the File
            /// </summary>
            public ulong MonoScriptSize;
        }

        /// <summary>
        /// A representation of an object in an asset file.
        /// </summary>
        [Serializable]
        public class ObjectData
        {
            /// <summary>
            /// FileId of Object in Asset File
            /// </summary>
            public long LocalIdentifierInFile;

            /// <summary>
            /// Object name within the Asset
            /// </summary>
            [SerializeField] internal string ObjectName;

            /// <summary>
            /// Component name if AssetType is a MonoBehaviour or Component
            /// </summary>
            [SerializeField] internal string ComponentName;

            /// <summary>
            /// Type of Object
            /// </summary>
            public AssetType AssetType;

            /// <summary>
            /// The size of the file on disk.
            /// </summary>
            public ulong SerializedSize;

            /// <summary>
            /// The size of the streamed Asset.
            /// </summary>
            public ulong StreamedSize;

            /// <summary>
            /// References to other Objects
            /// </summary>
            [SerializeField] internal List<ObjectReference> References = new List<ObjectReference>();
        }

        /// <summary>
        /// Identification of an Object within the same file
        /// </summary>
        [Serializable]
        internal class ObjectReference
        {
            public int AssetId;
            public List<int> ObjectIds;
        }

        /// <summary>
        /// Data store for Assets explicitly defined in an AssetBundle
        /// </summary>
        [Serializable]
        internal class ExplicitAsset
        {
            /// <summary>
            /// The Asset Guid.
            /// </summary>
            public AssetId Guid;

            /// <summary>
            /// The Asset path on disk
            /// </summary>
            public string AssetPath;

            /// <summary>
            /// Objects that consist of the overall asset
            /// </summary>
            public List<ObjectData> Objects = new List<ObjectData>();

            /// <summary>
            /// AssetType of the main Object for the Asset
            /// </summary>
            public AssetType MainAssetType;

            /// <summary>
            /// True if is a scene asset, else false
            /// </summary>
            public bool IsScene => AssetPath.EndsWith(".unity", StringComparison.Ordinal);

            /// <summary>
            /// The Addressable address defined in the Addressable Group window for an Asset.
            /// </summary>
            public string AddressableName;

            /// <summary>
            /// The size of the file on disk.
            /// </summary>
            public ulong SerializedSize;

            /// <summary>
            /// The size of the streamed Asset.
            /// </summary>
            public ulong StreamedSize;

            /// <summary>
            /// The file that the Asset was added to
            /// </summary>
            [SerializeReference]
            public File File;

            /// <summary>
            /// The AssetBundle that contains the asset
            /// </summary>
            [SerializeReference]
            public Bundle Bundle;

            /// <summary>
            /// List of data from other Assets referenced by an Asset in the File
            /// </summary>
            [SerializeReference]
            public List<DataFromOtherAsset> InternalReferencedOtherAssets = new List<DataFromOtherAsset>();

            /// <summary>
            /// List of explicit Assets referenced by this asset that are in the same AssetBundle
            /// </summary>
            [SerializeReference]
            public List<ExplicitAsset> InternalReferencedExplicitAssets = new List<ExplicitAsset>();

            /// <summary>
            /// List of explicit Assets referenced by this asset that are in a different AssetBundle
            /// </summary>
            [SerializeReference]
            public List<ExplicitAsset> ExternallyReferencedAssets = new List<ExplicitAsset>();

            /// <summary>
            /// List of Assets that reference this Asset
            /// </summary>
            [SerializeReference]
            internal List<ExplicitAsset> ReferencingAssets = new List<ExplicitAsset>();

            public string GetName() => $"{AddressableName} ({AssetPath})";
        }

        /// <summary>
        /// Data store for implicit Asset references
        /// </summary>
        [Serializable]
        internal class DataFromOtherAsset
        {
            /// <summary>
            /// The Guid of the Asset
            /// </summary>
            public AssetId AssetGuid;

            /// <summary>
            /// The Asset path on disk
            /// </summary>
            public string AssetPath;

            /// <summary>
            /// The file that the Asset was added to
            /// </summary>
            [SerializeReference]
            public File File;

            /// <summary>
            /// Objects that consist of the overall asset
            /// </summary>
            public List<ObjectData> Objects = new List<ObjectData>();

            /// <summary>
            /// AssetType of the main Object for the Asset
            /// </summary>
            public AssetType MainAssetType;

            /// <summary>
            /// True if is a scene asset, else false
            /// </summary>
            public bool IsScene => AssetPath.EndsWith(".unity", StringComparison.Ordinal);

            /// <summary>
            /// A list of Assets that reference this data
            /// </summary>
            [SerializeReference]
            public List<ExplicitAsset> ReferencingAssets = new List<ExplicitAsset>();

            /// <summary>
            /// The number of Objects in the data
            /// </summary>
            public int ObjectCount;

            /// <summary>
            /// The size of the data on disk
            /// </summary>
            public ulong SerializedSize;

            /// <summary>
            /// The size of the streamed data
            /// </summary>
            public ulong StreamedSize;
        }

        /// <summary>
        /// Data store for duplicated Implicit Asset information
        /// </summary>
        [Serializable]
        public class AssetDuplicationData
        {
            /// <summary>
            /// The Guid of the Asset with duplicates
            /// </summary>
            public AssetId AssetGuid;
            /// <summary>
            /// A list of duplicated objects and the bundles that contain them.
            /// </summary>
            public List<ObjectDuplicationData> DuplicatedObjects = new List<ObjectDuplicationData>();
        }

        /// <summary>
        /// Data store for duplicated Object information
        /// </summary>
        [Serializable]
        public class ObjectDuplicationData
        {
            /// <summary>
            /// The local identifier for an object.
            /// </summary>
            public long LocalIdentifierInFile;
            /// <summary>
            /// A list of bundles that include the referenced file.
            /// </summary>
            [SerializeReference] public List<File> IncludedInBundleFiles = new List<File>();
        }
    }

    /// <summary>
    /// Utility used to quickly reference data built with the build pipeline
    /// </summary>
    internal class LayoutLookupTables
    {
        /// <summary>
        /// The default AssetBundle name to the Bundle data map.
        /// </summary>
        public Dictionary<BundleKey, BuildLayout.Bundle> Bundles = new();

        /// <summary>
        /// File name to File data map.
        /// </summary>
        public Dictionary<string, BuildLayout.File> Files = new();

        internal Dictionary<BuildLayout.File, FileObjectData> FileToFileObjectData = new();

        /// <summary>
        /// Guid to ExplicitAsset data map.
        /// </summary>
        public Dictionary<AssetId, BuildLayout.ExplicitAsset> GuidToExplicitAsset = new();

        /// <summary>
        /// Group name to Group data map.
        /// </summary>
        public Dictionary<BundleKey, BuildLayout.Group> GroupLookup = new();


        /// Maps used for lookups while building the BuildLayout
        internal Dictionary<AssetId, List<BuildLayout.DataFromOtherAsset>> UsedImplicits = new();

        internal Dictionary<AssetId, AssetEntry> GuidToEntry = new();
        internal Dictionary<string, AssetType> AssetPathToTypeMap = new();
    }

    internal class FileObjectData
    {
        // id's for internal explicit asset and implicit asset
        public Dictionary<ObjectIdentifier, (int, int)> InternalObjectIds = new();

        public Dictionary<BuildLayout.ObjectData, ObjectIdentifier> Objects = new();

        public void Add(ObjectIdentifier buildObjectIdentifier, BuildLayout.ObjectData layoutObject, int assetId, int objectIndex)
        {
            InternalObjectIds[buildObjectIdentifier] = (assetId, objectIndex);
            Objects[layoutObject] = buildObjectIdentifier;
        }

        public bool TryGetObjectReferenceData(ObjectIdentifier obj, out (int, int) value)
        {
            if (!InternalObjectIds.TryGetValue(obj, out (int, int) data))
            {
                value = default;
                return false;
            }

            value = data;
            return true;
        }

        public bool TryGetObjectIdentifier(BuildLayout.ObjectData obj, out ObjectIdentifier objectIdOut)
        {
            if (!Objects.TryGetValue(obj, out objectIdOut))
            {
                objectIdOut = default;
                return false;
            }

            return true;
        }
    }
}