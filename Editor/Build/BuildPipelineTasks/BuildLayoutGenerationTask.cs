using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.Build.BuildPipelineTasks
{
    /// <summary>
    /// The BuildTask used to generate the bundle layout.
    /// </summary>
    internal class BuildLayoutGenerationTask : IBuildTask
    {
        private const bool k_PrettyPrint = false;

        internal static Action<string, BuildLayout> s_LayoutCompleteCallback;

        /// <summary>
        /// The GenerateLocationListsTask version.
        /// </summary>
        public int Version => 1;

#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        private AddressableAssetsBuildContext m_AaBuildContext;

        [InjectContext(ContextUsage.In)]
        private IBuildParameters m_Parameters;

        [InjectContext]
        private IBundleWriteData m_WriteData;

        [InjectContext]
        private IBuildResults m_Results;

        [InjectContext(ContextUsage.In)]
        private IDependencyData m_DependencyData;

        [InjectContext(ContextUsage.In)]
        private IObjectDependencyData m_ObjectDependencyData;

        [InjectContext(ContextUsage.In)]
        private IBundleBuildResults m_BuildBundleResults;
#pragma warning restore 649

        private static string GetLayoutFilePathForFormat()
        {
            return $"{PathConfig.LibraryPath}buildlayout.txt";
        }

        private static string TimeStampedReportPath(DateTime now)
        {
            var timestamp = $"{now.Year:D4}.{now.Month:D2}.{now.Day:D2}.{now.Hour:D2}.{now.Minute:D2}.{now.Second:D2}";
            return $"{PathConfig.BuildReportPath}buildlayout_{timestamp}.json";
        }

        private static AssetBucket GetOrCreate(Dictionary<AssetId, AssetBucket> buckets, AssetId asset)
        {
            return buckets.TryGetValue(asset, out var bucket) ? bucket
                : buckets[asset] = new AssetBucket(asset);
        }

        private class AssetBucket
        {
            public AssetId guid;
            public bool isFilePathBucket => guid.IsPath;
            public List<ObjectSerializedInfo> objs = new();
            public BuildLayout.ExplicitAsset ExplicitAsset;


            public AssetBucket(AssetId guid)
            {
                this.guid = guid;
            }

            public string ResolveAssetPath()
            {
                return isFilePathBucket ? guid.Value : AssetDatabase.GUIDToAssetPath(guid.Value);
            }

            public ulong CalcObjectSize()
            {
                ulong sum = 0;
                foreach (var obj in objs)
                    sum += obj.header.size;
                return sum;
            }

            public ulong CalcStreamedSize()
            {
                ulong sum = 0;
                foreach (var obj in objs)
                    sum += obj.rawData.size;
                return sum;
            }
        }

        private static AssetType GetSceneObjectType(string name)
        {
            return Enum.TryParse<AssetType>(name, true, out var rst)
                ? rst : AssetType.SceneObject;
        }

        private static ulong GetFileSizeFromPath(string path, out bool success)
        {
            var fileInfo = new FileInfo(path);
            if (fileInfo.Exists)
            {
                success = true;
                return (ulong) fileInfo.Length;
            }
            else
            {
                success = false;
                return 0;
            }
        }

        private BuildLayout CreateBuildLayout()
        {
            var aaContext = m_AaBuildContext;
            L.I("Generate Lookup tables");
            var lookup = GenerateLookupTables(aaContext);
            L.I("Generate Build Layout");
            return GenerateBuildLayout(aaContext, lookup);
        }

        private LayoutLookupTables GenerateLookupTables(AddressableAssetsBuildContext aaContext)
        {
            var lookup = new LayoutLookupTables();

            var objectTypes = new Dictionary<ObjectIdentifier, Type[]>(1024);
            foreach (var assetResult in m_Results.AssetResults)
            foreach (var resultEntry in assetResult.Value.ObjectTypes)
            {
                if (!objectTypes.ContainsKey(resultEntry.Key))
                    objectTypes.Add(resultEntry.Key, resultEntry.Value);
            }

            var bundleKeyToId = ResourceCatalogBuilder.AssignBundleId(aaContext.entries.Values);
            foreach (var bundleName in m_WriteData.FileToBundle.Values.Distinct())
            {
                var bundleKey = BundleKey.FromBuildName(bundleName);
                var bundle = new BuildLayout.Bundle(bundleKey, bundleKeyToId[bundleKey]);
                lookup.Bundles.Add(bundleKey, bundle);
            }

            foreach (var b in lookup.Bundles.Values)
            {
                if (aaContext.bundleToImmediateBundleDependencies.TryGetValue(b.Key, out var deps))
                    b.Dependencies = deps.Select(x => lookup.Bundles[x]).Where(x => b != x).ToList();
                if (aaContext.bundleToExpandedBundleDependencies.TryGetValue(b.Key, out var deps2))
                    b.ExpandedDependencies = deps2.Select(x => lookup.Bundles[x]).Where(x => b != x).ToList();
            }

            // create files
            foreach (var (fileName, bundleName) in m_WriteData.FileToBundle)
            {
                var bundle = lookup.Bundles[BundleKey.FromBuildName(bundleName)];
                var f = new BuildLayout.File
                {
                    Name = fileName,
                    Bundle = bundle
                };

                var result = m_Results.WriteResults[f.Name];
                foreach (var rf in result.resourceFiles)
                {
                    var sf = new BuildLayout.SubFile();
                    sf.IsSerializedFile = rf.serializedFile;
                    sf.Name = rf.fileAlias;
                    sf.Size = GetFileSizeFromPath(rf.fileName, out bool success);
                    if (!success)
                        L.W($"Resource File {sf.Name} from file  \"{f.Name}\" was detected as part of the build, but the file could not be found. This may be because your build cache size is too small. Filesize of this Resource File will be 0 in BuildLayout.");

                    f.SubFiles.Add(sf);
                }

                bundle.Files.Add(f);
                lookup.Files.Add(f.Name, f);
            }

            // create assets
            foreach (var (guid, bundles) in m_WriteData.AssetToFiles)
            {
                var file = lookup.Files[bundles[0]];
                var a = new BuildLayout.ExplicitAsset();
                a.Guid = guid;
                a.AssetPath = AssetDatabase.GUIDToAssetPath(guid);
                a.File = file;
                a.Bundle = file.Bundle;
                file.Assets.Add(a);
                lookup.GuidToExplicitAsset.Add(a.Guid, a);
            }

            var guidToPulledInBuckets = new Dictionary<AssetId, List<BuildLayout.DataFromOtherAsset>>();

            foreach (var file in lookup.Files.Values)
            {
                var buckets = new Dictionary<AssetId, AssetBucket>();
                var writeResult = m_Results.WriteResults[file.Name];
                var sceneObjects = new List<ObjectSerializedInfo>();
                var fData = new FileObjectData();
                lookup.FileToFileObjectData.Add(file, fData);

                foreach (var info in writeResult.serializedObjects)
                {
                    if (info.serializedObject.guid.Empty())
                    {
                        if (info.serializedObject.filePath.Equals("temp:/assetbundle", StringComparison.OrdinalIgnoreCase))
                        {
                            file.BundleObjectInfo = new BuildLayout.AssetBundleObjectInfo();
                            file.BundleObjectInfo.Size = info.header.size;
                            continue;
                        }
                        if (info.serializedObject.filePath.StartsWith("temp:/preloaddata", StringComparison.OrdinalIgnoreCase))
                        {
                            file.PreloadInfoSize = (int) info.header.size;
                            continue;
                        }
                        if (info.serializedObject.filePath.StartsWith("temp:/", StringComparison.OrdinalIgnoreCase))
                        {
                            sceneObjects.Add(info);
                            continue;
                        }

                        Assert.IsFalse(!string.IsNullOrEmpty(info.serializedObject.filePath), "Empty file path for object");
                        var pathBucket = GetOrCreate(buckets, AssetId.FromPath(info.serializedObject.filePath));
                        pathBucket.objs.Add(info);
                        continue;
                    }

                    var bucket = GetOrCreate(buckets, info.serializedObject.guid);
                    bucket.objs.Add(info);
                }

                if (sceneObjects.Count > 0)
                {
                    var sceneAsset = file.Assets.First(x => x.AssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));
                    AssetBucket bucket = GetOrCreate(buckets, sceneAsset.Guid);
                    bucket.objs.AddRange(sceneObjects);
                }

                // Update buckets with a reference to their explicit asset
                file.Assets.ForEach(eAsset =>
                {
                    if (!buckets.TryGetValue(eAsset.Guid, out AssetBucket b))
                        b = GetOrCreate(buckets, eAsset.Guid); // some assets might not pull in any objects
                    b.ExplicitAsset = eAsset;
                });

                // Create entries for buckets that are implicitly pulled in
                var guidToOtherData = new Dictionary<AssetId, BuildLayout.DataFromOtherAsset>();
                int assetInFileId = 0;
                var MonoScriptAssets = new HashSet<AssetId>();

                foreach (var bucket in buckets.Values.Where(x => x.ExplicitAsset == null))
                {
                    string assetPath = bucket.ResolveAssetPath();
                    if (assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                        assetPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        file.MonoScriptCount++;
                        file.MonoScriptSize += bucket.CalcObjectSize();
                        MonoScriptAssets.Add(bucket.guid);
                        continue;
                    }

                    var implicitAsset = new BuildLayout.DataFromOtherAsset();
                    implicitAsset.AssetPath = assetPath;
                    implicitAsset.AssetGuid = bucket.guid;
                    implicitAsset.SerializedSize = bucket.CalcObjectSize();
                    implicitAsset.StreamedSize = bucket.CalcStreamedSize();
                    implicitAsset.ObjectCount = bucket.objs.Count;
                    implicitAsset.File = file;
                    assetInFileId = file.OtherAssets.Count;
                    file.OtherAssets.Add(implicitAsset);

                    if (lookup.UsedImplicits.TryGetValue(implicitAsset.AssetGuid, out var dataList))
                        dataList.Add(implicitAsset);
                    else
                        lookup.UsedImplicits.Add(implicitAsset.AssetGuid, new List<BuildLayout.DataFromOtherAsset>() { implicitAsset });

                    guidToOtherData[implicitAsset.AssetGuid] = implicitAsset;

                    if (lookup.AssetPathToTypeMap.TryGetValue(implicitAsset.AssetPath, out var assetType))
                        implicitAsset.MainAssetType = assetType;
                    else
                    {
                        implicitAsset.MainAssetType = BuildLayoutHelpers.GetAssetType(AssetDatabase.GetMainAssetTypeAtPath(implicitAsset.AssetPath));
                        lookup.AssetPathToTypeMap[implicitAsset.AssetPath] = implicitAsset.MainAssetType;
                    }

                    var localIdentifierToObjectName = GetObjectsIdForAsset(assetPath);

                    foreach (ObjectSerializedInfo bucketObj in bucket.objs)
                    {
                        Type objType = null;
                        if (objectTypes.TryGetValue(bucketObj.serializedObject, out Type[] types) && types.Length > 0)
                            objType = types[0];

                        var eType = objType == null ? AssetType.Other : BuildLayoutHelpers.GetAssetType(objType);
                        if (implicitAsset.IsScene)
                        {
                            if (eType == AssetType.Other)
                                eType = AssetType.SceneObject;
                        }

                        string name = "";
                        if (localIdentifierToObjectName.TryGetValue(bucketObj.serializedObject.localIdentifierInFile, out string value))
                            name = value;
                        var layoutObject = new BuildLayout.ObjectData()
                        {
                            ObjectName = name,
                            LocalIdentifierInFile = bucketObj.serializedObject.localIdentifierInFile,
                            AssetType = eType,
                            SerializedSize = bucketObj.header.size,
                            StreamedSize = bucketObj.rawData.size
                        };

                        int objectIndex = implicitAsset.Objects.Count;
                        implicitAsset.Objects.Add(layoutObject);
                        fData.Add(bucketObj.serializedObject, layoutObject, assetInFileId, objectIndex);
                    }

                    if (!guidToPulledInBuckets.TryGetValue(implicitAsset.AssetGuid,
                            out List<BuildLayout.DataFromOtherAsset> bucketList))
                        bucketList = guidToPulledInBuckets[implicitAsset.AssetGuid] = new List<BuildLayout.DataFromOtherAsset>();
                    bucketList.Add(implicitAsset);
                }

                assetInFileId = file.OtherAssets.Count - 1;
                foreach (var asset in file.Assets)
                {
                    assetInFileId++;
                    var bucket = buckets[asset.Guid];

                    // size info
                    asset.SerializedSize = bucket.CalcObjectSize();
                    asset.StreamedSize = bucket.CalcStreamedSize();

                    // asset type
                    if (lookup.AssetPathToTypeMap.TryGetValue(asset.AssetPath, out var assetType))
                        asset.MainAssetType = assetType;
                    else
                    {
                        var type = AssetDatabase.GetMainAssetTypeAtPath(asset.AssetPath);
                        asset.MainAssetType = BuildLayoutHelpers.GetAssetType(type);
                        lookup.AssetPathToTypeMap[asset.AssetPath] = asset.MainAssetType;
                    }

                    if (asset.MainAssetType == AssetType.GameObject)
                    {
                        var importerType = AssetDatabase.GetImporterType(asset.AssetPath);
                        if (importerType == typeof(ModelImporter))
                            asset.MainAssetType = AssetType.Model;
                        else if (importerType != null)
                            asset.MainAssetType = AssetType.Prefab;
                    }

                    if (asset.IsScene)
                    {
                        CollectObjectsForScene(bucket, asset);
                    }
                    else
                    {
                        var localIdentifierToObjectName = GetObjectsIdForAsset(asset.AssetPath);
                        CollectObjectsForAsset(bucket, objectTypes, asset, localIdentifierToObjectName, fData, assetInFileId);
                    }
                }

                var explicitAssetsAddedAsExternal = new HashSet<BuildLayout.ExplicitAsset>();
                // Add references
                foreach (var asset in file.Assets)
                {
                    IEnumerable<ObjectIdentifier> refs = null;
                    if (m_DependencyData.AssetInfo.TryGetValue((GUID) asset.Guid, out AssetLoadInfo info))
                        refs = info.referencedObjects;
                    else
                        refs = m_DependencyData.SceneInfo[(GUID) asset.Guid].referencedObjects;

                    foreach (var refGUID in refs.Select(x => x.guid.Empty() ? AssetId.FromPath(x.filePath) : x.guid).Distinct())
                    {
                        if (MonoScriptAssets.Contains(refGUID))
                            continue;
                        if (guidToOtherData.TryGetValue(refGUID, out BuildLayout.DataFromOtherAsset dfoa))
                        {
                            dfoa.ReferencingAssets.Add(asset);
                            asset.InternalReferencedOtherAssets.Add(dfoa);
                        }
                        else if (buckets.TryGetValue(refGUID, out AssetBucket refBucket) && refBucket.ExplicitAsset != null)
                        {
                            refBucket.ExplicitAsset.ReferencingAssets.Add(asset);
                            asset.InternalReferencedExplicitAssets.Add(refBucket.ExplicitAsset);
                        }
                        else if (lookup.GuidToExplicitAsset.TryGetValue(refGUID, out BuildLayout.ExplicitAsset refAsset))
                        {
                            refAsset.ReferencingAssets.Add(asset);
                            asset.ExternallyReferencedAssets.Add(refAsset);
                            if (explicitAssetsAddedAsExternal.Add(refAsset))
                                file.ExternalReferences.Add(refAsset);
                        }
                    }
                }
            }

            foreach (BuildLayout.File file in lookup.Files.Values)
            {
                if (lookup.FileToFileObjectData.TryGetValue(file, out FileObjectData fData))
                {
                    foreach (BuildLayout.ExplicitAsset asset in file.Assets)
                    {
                        if (asset.IsScene && asset.Objects.Count > 0)
                        {
                            BuildLayout.ObjectData objectData = asset.Objects[0];
                            IEnumerable<ObjectIdentifier> dependencies = m_DependencyData.SceneInfo[(GUID) asset.Guid].referencedObjects;
                            CollectObjectReferences(fData, objectData, file, lookup, dependencies);
                        }
                        else
                        {
                            foreach (BuildLayout.ObjectData objectData in asset.Objects)
                                CollectObjectReferences(fData, objectData, file, lookup);
                        }
                    }

                    foreach (var otherAsset in file.OtherAssets)
                    {
                        foreach (BuildLayout.ObjectData objectData in otherAsset.Objects)
                        {
                            // TODO see if theres a cached result for this
                            CollectObjectReferences(fData, objectData, file, lookup);
                        }
                    }
                }
            }

            return lookup;
        }

        private static Dictionary<long, string> GetObjectsIdForAsset(string assetPath)
        {
            var assetSubObjects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            Assert.IsNotNull(assetSubObjects, $"Failed to load assets for {assetPath}");

            var localIdentifierToObjectName = new Dictionary<long, string>(assetSubObjects.Length);
            foreach (var o in assetSubObjects)
            {
                if (o != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out _, out long localId))
                    localIdentifierToObjectName[localId] = o.name;
            }
            return localIdentifierToObjectName;
        }

        private static void CollectObjectsForAsset(in AssetBucket bucket, in Dictionary<ObjectIdentifier, Type[]> objectTypes, BuildLayout.ExplicitAsset asset,
            in Dictionary<long, string> localIdentifierToObjectName, FileObjectData fileObjectData, int assetInFileId)
        {
            foreach (ObjectSerializedInfo bucketObj in bucket.objs)
            {
                Type objType = null;
                if (objectTypes.TryGetValue(bucketObj.serializedObject, out Type[] types) && types.Length > 0)
                    objType = types[0];

                var eType = objType == null ? AssetType.Other : BuildLayoutHelpers.GetAssetType(objType);
                if (IsComponentType(eType)) eType = AssetType.Component;
                var componentName = "";

                if (asset.IsScene && eType is AssetType.Other or AssetType.Component)
                    eType = GetSceneObjectType(bucketObj.serializedObject.filePath.Remove(0, 6));
                if (eType == AssetType.Component)
                    componentName = objType.Name;

                var name = "";
                if (localIdentifierToObjectName.TryGetValue(bucketObj.serializedObject.localIdentifierInFile, out var value))
                    name = value;

                var layoutObject = new BuildLayout.ObjectData()
                {
                    ObjectName = name,
                    ComponentName = componentName,
                    LocalIdentifierInFile = bucketObj.serializedObject.localIdentifierInFile,
                    AssetType = eType,
                    SerializedSize = bucketObj.header.size,
                    StreamedSize = bucketObj.rawData.size
                };

                int objectIndex = asset.Objects.Count;
                asset.Objects.Add(layoutObject);
                fileObjectData.Add(bucketObj.serializedObject, layoutObject, assetInFileId, objectIndex);
            }
        }

        private static bool IsComponentType(AssetType eType)
        {
            // old components that should not have been in the enum, treat all as component type
            return eType
                is AssetType.Transform
                or AssetType.GameObject
                or AssetType.Camera
                or AssetType.Light
                or AssetType.MeshFilter
                or AssetType.MeshRenderer
                or AssetType.SphereCollider
                or AssetType.AudioListener
                or AssetType.BoxCollider
                or AssetType.BoxCollider2D
                or AssetType.MonoBehaviour;
        }

        private void CollectObjectsForScene(in AssetBucket bucket, BuildLayout.ExplicitAsset asset)
        {
            Dictionary<AssetType, BuildLayout.ObjectData> TypeToObjectData = new Dictionary<AssetType, BuildLayout.ObjectData>();
            foreach (ObjectSerializedInfo bucketObj in bucket.objs)
            {
                AssetType eType = GetSceneObjectType(bucketObj.serializedObject.filePath.Remove(0, 6));
                if (!TypeToObjectData.TryGetValue(eType, out BuildLayout.ObjectData layoutObject))
                {
                    layoutObject = new BuildLayout.ObjectData()
                    {
                        ObjectName = eType.ToString(),
                        LocalIdentifierInFile = TypeToObjectData.Count + 1,
                        AssetType = eType,
                        SerializedSize = bucketObj.header.size,
                        StreamedSize = bucketObj.rawData.size
                    };
                    TypeToObjectData.Add(eType, layoutObject);
                }
                else
                {
                    layoutObject.SerializedSize += bucketObj.header.size;
                    layoutObject.StreamedSize += bucketObj.rawData.size;
                }
            }

            // main scene object
            asset.Objects.Add(new BuildLayout.ObjectData()
            {
                ObjectName = "Main",
                LocalIdentifierInFile = 0,
                AssetType = AssetType.SceneObject,
                SerializedSize = 0,
                StreamedSize = 0
            });

            foreach (BuildLayout.ObjectData layoutObject in TypeToObjectData.Values)
            {
                asset.Objects.Add(layoutObject);
            }
        }

        private void CollectObjectReferences(FileObjectData fileObjectLookup, BuildLayout.ObjectData objectData, BuildLayout.File fileData, LayoutLookupTables lookup,
            IEnumerable<ObjectIdentifier> dependencies = null)
        {
            // get the ObjectIdentification object for the objectData
            if (dependencies == null && fileObjectLookup.TryGetObjectIdentifier(objectData, out var objId))
            {
                m_ObjectDependencyData.ObjectDependencyMap.TryGetValue(objId, out List<ObjectIdentifier> dependenciesFromMap);
                dependencies = dependenciesFromMap;
            }

            if (dependencies != null)
            {
                int assetIdOffset = fileData.Assets.Count + fileData.OtherAssets.Count;
                Dictionary<int, HashSet<int>> assetIndices = new Dictionary<int, HashSet<int>>(); // TODO I don't like this is allocated so much
                HashSet<int> indices;
                foreach (ObjectIdentifier dependency in dependencies)
                {
                    if (fileObjectLookup.TryGetObjectReferenceData(dependency, out (int, int) val))
                    {
                        // object dependency within this file was found
                        if (assetIndices.TryGetValue(val.Item1, out indices))
                            indices.Add(val.Item2);
                        else
                            assetIndices[val.Item1] = new HashSet<int>() { val.Item2 };
                    }
                    else // if not in fileObjectLookup, not a dependency on this file, need to find in another file
                    {
                        if (lookup.GuidToExplicitAsset.TryGetValue(dependency.guid, out BuildLayout.ExplicitAsset referencedAsset))
                        {
                            var otherFData = lookup.FileToFileObjectData[referencedAsset.File];
                            if (otherFData.TryGetObjectReferenceData(dependency, out val))
                            {
                                val.Item1 = -1;
                                for (int i = 0; i < fileData.ExternalReferences.Count; ++i)
                                {
                                    if (fileData.ExternalReferences[i] == referencedAsset)
                                    {
                                        val.Item1 = i + assetIdOffset;
                                        break;
                                    }
                                }

                                if (val.Item1 >= 0)
                                {
                                    if (assetIndices.TryGetValue(val.Item1, out indices))
                                        indices.Add(val.Item2);
                                    else
                                        assetIndices[val.Item1] = new HashSet<int>() { val.Item2 };
                                }
                            }
                        } // can be false for built in shared bundles
                    }
                }

                foreach (KeyValuePair<int, HashSet<int>> assetRefData in assetIndices)
                {
                    objectData.References.Add(new BuildLayout.ObjectReference() { AssetId = assetRefData.Key, ObjectIds = new List<int>(assetRefData.Value) });
                }
            }
        }

        private BuildLayout GenerateBuildLayout(AddressableAssetsBuildContext aaContext, LayoutLookupTables lookup)
        {
            BuildLayout layout = new BuildLayout();
            layout.BuildStart = aaContext.buildStartTime;

            AddressableCatalog aaCatalog = aaContext.Catalog;

            L.I("Generate Basic Information");
            SetLayoutMetaData(layout);

            // Map from GUID to AddrssableAssetEntry
            lookup.GuidToEntry = aaContext.entries.ToDictionary(
                kvp => (AssetId) kvp.Key,
                kvp => aaCatalog.GetEntryByGUID(kvp.Value.GUID));

            // create groups
            foreach (var (bundle, group) in aaContext.bundleToAssetGroup)
            {
                var grp = new BuildLayout.Group();
                grp.Name = group.Name;
                lookup.GroupLookup.Add(bundle, grp);
                layout.Groups.Add(grp);
            }

            {
                L.I("Correlate Bundles to groups");
                foreach (var b in lookup.Bundles.Values)
                    CorrelateBundleToAssetGroup(layout, b, lookup, aaContext);
            }

            L.I("Apply Addressable info to layout data");
            ApplyAddressablesInformationToExplicitAssets(layout, lookup);
            L.I("Process additional bundle data");
            PostProcessBundleData(lookup);
            L.I("Generating implicit inclusion data");
            AddImplicitAssetsToLayout(lookup, layout);

            SetDuration(layout);
            return layout;
        }

        private void CorrelateBundleToAssetGroup(BuildLayout layout, BuildLayout.Bundle b, LayoutLookupTables lookup, AddressableAssetsBuildContext aaContext)
        {
            if (aaContext.Catalog.TryGetGroup(b.Key.GetBuildName(), out _))
            {
                var assetGroup = lookup.GroupLookup[b.Key];
                b.Group = assetGroup;
                Assert.IsNull(assetGroup.Bundle);
                assetGroup.Bundle = b;
            }
            // bundleToAssetGroup doesn't contain the builtin bundles. The builtin content is built using values from the default group
            else
            {
                var defaultGroup = aaContext.Catalog.DefaultGroup;
                b.Group = lookup.GroupLookup.Values.Single(g => g.Name == defaultGroup.Name);
                layout.BuiltInBundles.Add(b);
            }

            var filePath = PathConfig.GetAssetBundleLoadPath(b.Id);
            b.FileSize = GetFileSizeFromPath(filePath, out bool success);
            if (!success)
                L.W($"AssetBundle {b.Key} from Addressable Group \"{b.Group.Name}\" was detected as part of the build, but the file could not be found. Filesize of this AssetBundle will be 0 in BuildLayout.");
        }

        private void PostProcessBundleData(LayoutLookupTables lookup)
        {
            var rootBundles = new HashSet<BuildLayout.Bundle>(lookup.Bundles.Values);
            foreach (var b in lookup.Bundles.Values)
                GenerateBundleDependencyAndEfficiencyInformation(b, rootBundles);

            CalculateBundleEfficiencies(rootBundles);
        }

        internal static void CalculateBundleEfficiencies(IEnumerable<BuildLayout.Bundle> rootBundles)
        {
            Dictionary<BuildLayout.Bundle.BundleDependency, BuildLayout.Bundle.EfficiencyInfo> bundleDependencyCache = new Dictionary<BuildLayout.Bundle.BundleDependency, BuildLayout.Bundle.EfficiencyInfo>();
            foreach (BuildLayout.Bundle b in rootBundles)
                CalculateEfficiency(b, bundleDependencyCache);
        }

        /// <summary>
        /// Calculates the Efficiency of bundle and all bundles below it in the dependency tree and caches the results.
        /// Example: There are 3 bundles A, B, and C, that are each 10 MB on disk. A depends on 2 MB worth of assets in B, and B depends on 4 MB worth of assets in C.
        /// The Efficiency of the dependencyLink from A->B would be 2/10 -> 20% and the ExpandedEfficiency of A->B would be (2 + 4)/(10 + 10) -> 6/20 -> 30%
        /// </summary>
        /// <param name="bundle"> the root of the dependency tree that the CalculateEfficiency call will start from. </param>
        /// <param name="bundleDependencyCache"> Cache of all bundle dependencies that have already been calculated </param>
        internal static void CalculateEfficiency(BuildLayout.Bundle bundle, Dictionary<BuildLayout.Bundle.BundleDependency, BuildLayout.Bundle.EfficiencyInfo> bundleDependencyCache = null)
        {
            Stack<BuildLayout.Bundle.BundleDependency> stk = new Stack<BuildLayout.Bundle.BundleDependency>();
            Queue<BuildLayout.Bundle> q = new Queue<BuildLayout.Bundle>();
            HashSet<BuildLayout.Bundle> seenBundles = new HashSet<BuildLayout.Bundle>();

            if (bundleDependencyCache == null)
                bundleDependencyCache = new Dictionary<BuildLayout.Bundle.BundleDependency, BuildLayout.Bundle.EfficiencyInfo>();

            q.Enqueue(bundle);

            // Populate the stack of BundleDependencies with the lowest depth BundleDependencies being at the top of the stack
            while (q.Count > 0)
            {
                var curBundle = q.Dequeue();
                foreach (var bd in curBundle.BundleDependencies)
                {
                    if (bundleDependencyCache.ContainsKey(bd))
                        break;

                    if (!seenBundles.Contains(curBundle))
                    {
                        q.Enqueue(bd.DependencyBundle);
                        stk.Push(bd);
                    }
                }
                seenBundles.Add(curBundle);
            }

            // Get the required information out of each BundleDependency, caching the necessary info for each as you work your way up the tree
            while (stk.Count > 0)
            {
                var curBd = stk.Pop();

                ulong totalReferencedAssetFilesize = 0;
                ulong totalDependentAssetFilesize = 0;
                foreach (var bd in curBd.DependencyBundle.BundleDependencies)
                {
                    if (bundleDependencyCache.TryGetValue(bd, out var ei))
                    {
                        totalReferencedAssetFilesize += ei.referencedAssetFileSize;
                        totalDependentAssetFilesize += ei.totalAssetFileSize;
                    }
                }

                var newEfficiencyInfo = new BuildLayout.Bundle.EfficiencyInfo()
                {
                    referencedAssetFileSize = curBd.referencedAssetsFileSize + totalReferencedAssetFilesize,
                    totalAssetFileSize = curBd.DependencyBundle.FileSize + totalDependentAssetFilesize,
                };

                curBd.Efficiency = newEfficiencyInfo.totalAssetFileSize > 0 ? (float) curBd.referencedAssetsFileSize / curBd.DependencyBundle.FileSize : 1f;
                curBd.ExpandedEfficiency = newEfficiencyInfo.totalAssetFileSize > 0 ? (float) newEfficiencyInfo.referencedAssetFileSize / newEfficiencyInfo.totalAssetFileSize : 1f;
                bundleDependencyCache[curBd] = newEfficiencyInfo;
            }
        }

        private void ApplyAddressablesInformationToExplicitAssets(BuildLayout layout, LayoutLookupTables lookup)
        {
            foreach (var bundle in BuildLayoutHelpers.EnumerateBundles(layout))
            {
                for (int fileIndex = 0; fileIndex < bundle.Files.Count; ++fileIndex)
                {
                    foreach (BuildLayout.ExplicitAsset rootAsset in bundle.Files[fileIndex].Assets)
                    {
                        if (lookup.GuidToEntry.TryGetValue(rootAsset.Guid, out AssetEntry rootEntry))
                        {
                            ApplyAddressablesInformationToExplicitAsset(rootAsset, rootEntry);
                        }
                    }
                }
            }
        }

        private static void ApplyAddressablesInformationToExplicitAsset(BuildLayout.ExplicitAsset rootAsset, AssetEntry rootEntry)
        {
            rootAsset.AddressableName = rootEntry.Address;
            rootAsset.MainAssetType = BuildLayoutHelpers.GetAssetType(rootEntry.Asset.GetType());

            if (rootAsset.Bundle == null)
            {
                Debug.LogError($"Failed to get bundle information for AddressableAssetEntry: {rootEntry.GUID.Value}");
                return;
            }

            foreach (BuildLayout.ExplicitAsset referencedAsset in rootAsset.ExternallyReferencedAssets)
            {
                if (referencedAsset.Bundle == null)
                {
                    Debug.LogError($"Failed to get bundle information for AddressableAssetEntry: {rootEntry.GUID.Value}");
                    continue;
                }

                // Create the dependency between rootAssets bundle and referenced Assets bundle,
                rootAsset.Bundle.UpdateBundleDependency(rootAsset, referencedAsset);
            }
        }

        private void GenerateBundleDependencyAndEfficiencyInformation(BuildLayout.Bundle b, HashSet<BuildLayout.Bundle> rootBundles)
        {
            b.ExpandedDependencyFileSize = 0;
            b.DependencyFileSize = 0;
            foreach (var dependency in b.Dependencies)
            {
                dependency.DependentBundles.Add(b);
                rootBundles.Remove(dependency);
                b.DependencyFileSize += dependency.FileSize;
            }

            foreach (var expandedDependency in b.ExpandedDependencies)
                b.ExpandedDependencyFileSize += expandedDependency.FileSize;

            foreach (var file in b.Files)
                b.AssetCount += file.Assets.Count;

            b.SerializeBundleToBundleDependency();
        }

        private void AddImplicitAssetsToLayout(LayoutLookupTables lookup, BuildLayout layout)
        {
            foreach (var pair in lookup.UsedImplicits)
            {
                if (pair.Value.Count <= 1)
                    continue;

                var assetDuplication = new BuildLayout.AssetDuplicationData();
                assetDuplication.AssetGuid = pair.Key;
                bool hasDuplicatedObjects = false;

                foreach (BuildLayout.DataFromOtherAsset implicitData in pair.Value)
                {
                    foreach (BuildLayout.ObjectData objectData in implicitData.Objects)
                    {
                        var existing = assetDuplication.DuplicatedObjects.Find(data => data.LocalIdentifierInFile == objectData.LocalIdentifierInFile);
                        if (existing != null)
                            existing.IncludedInBundleFiles.Add(implicitData.File);
                        else
                        {
                            assetDuplication.DuplicatedObjects.Add(
                                new BuildLayout.ObjectDuplicationData()
                                {
                                    IncludedInBundleFiles = new List<BuildLayout.File> { implicitData.File },
                                    LocalIdentifierInFile = objectData.LocalIdentifierInFile
                                });
                            hasDuplicatedObjects = true;
                        }
                    }
                }

                if (!hasDuplicatedObjects)
                    continue;

                for (int i = assetDuplication.DuplicatedObjects.Count - 1; i >= 0; --i)
                {
                    if (assetDuplication.DuplicatedObjects[i].IncludedInBundleFiles.Count <= 1)
                        assetDuplication.DuplicatedObjects.RemoveAt(i);
                }

                if (assetDuplication.DuplicatedObjects.Count > 0)
                    layout.DuplicatedAssets.Add(assetDuplication);
            }
        }

        private static void SetDuration(BuildLayout layout)
        {
            var duration = DateTime.Now - layout.BuildStart;
            layout.Duration = duration.TotalSeconds;
        }

        private static void SetLayoutMetaData(BuildLayout layoutOut)
        {
            layoutOut.UnityVersion = Application.unityVersion;
            layoutOut.BuildTarget = EditorUserBuildSettings.activeBuildTarget;
        }

        /// <summary>
        /// Runs the build task with the injected context.
        /// </summary>
        /// <returns>The success or failure ReturnCode</returns>
        public ReturnCode Run()
        {
            var layout = CreateBuildLayout();

            L.I("Writing BuildReport File");
            var destinationPath = TimeStampedReportPath(layout.BuildStart);
            layout.WriteToFile(destinationPath, k_PrettyPrint);

            {
                L.I("Writing Layout Text File");
                var txtFilePath = GetLayoutFilePathForFormat();
                using var s = File.Open(txtFilePath, FileMode.Create);
                BuildLayoutPrinter.WriteBundleLayout(s, layout);
                L.I($"Text build layout written to {txtFilePath} and json build layout written to {destinationPath}");
            }

            ProjectConfigData.AddBuildReportFilePath(destinationPath);
            s_LayoutCompleteCallback?.Invoke(destinationPath, layout);
            return ReturnCode.Success;
        }

        /// <summary>
        /// Creates an Error report for the error provided
        /// </summary>
        /// <param name="error">Build error string</param>
        /// <param name="aaContext">The current build context</param>
        public static void GenerateErrorReport(string error, AddressableAssetsBuildContext aaContext)
        {
            Assert.IsNotNull(aaContext);
            Assert.IsNotNull(aaContext.Catalog);

            var layout = new BuildLayout();
            layout.BuildStart = aaContext.buildStartTime;
            layout.BuildError = error;
            SetLayoutMetaData(layout);

            string destinationPath = TimeStampedReportPath(layout.BuildStart);
            layout.WriteToFile(destinationPath, k_PrettyPrint);
        }
    }
}