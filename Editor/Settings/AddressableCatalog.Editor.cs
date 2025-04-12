using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using FuzzySearch = Sirenix.Utilities.Editor.FuzzySearch;

namespace UnityEditor.AddressableAssets
{
    public partial class AddressableCatalog
    {
        [ShowInInspector]
        private static string _searchPattern;

        [ShowInInspector, LabelText("Groups"), HideReferenceObjectPicker]
        [ListDrawerSettings(ShowPaging = false,
            CustomAddFunction = nameof(AddNewGroup),
            CustomRemoveElementFunction = nameof(RemoveGroup))]
        [OnValueChanged(nameof(ClearCache), includeChildren: true)]
        private AssetGroup[] _normalGroups
        {
            get => FilterGroups(Groups, _searchPattern, false);
            set => throw new NotSupportedException(); // placeholder for odin inspector
        }

        [ShowInInspector, LabelText("Generated Groups"), HideReferenceObjectPicker]
        [ListDrawerSettings(DefaultExpandedState = false, DraggableItems = false, ShowPaging = false)]
        private AssetGroup[] _generatedGroups => FilterGroups(Groups, _searchPattern, true);

        private void AddNewGroup()
        {
            if (string.IsNullOrEmpty(_searchPattern))
                throw new Exception("Search pattern is empty. Please set a search pattern.");

            // Issue new bundle id
            var bundleIdStart = (int) AssetBundleId.BuiltInShaders + 1;
            var bundleIdMax = (int) AssetBundleIdUtils.MaxForNormalBundle();
            var bundleIdCandidates = Enumerable.Range(bundleIdStart, bundleIdMax - bundleIdStart + 1).ToHashSet();
            foreach (var group in Groups) bundleIdCandidates.Remove((int) group.BundleId);
            var assetBundleId = (AssetBundleId) bundleIdCandidates.First();

            // Insert index = first non-generated group
            var i = Groups.Length - 1;
            for (; i >= 0; i--)
            {
                if (!Groups[i].IsGenerated)
                    break;
            }
            var insertIndex = i + 1;

            // Create new group
            var newGroup = new AssetGroup(_searchPattern, Array.Empty<AssetEntry>()) { BundleId = assetBundleId };
            var groups = Groups.ToList();
            groups.Insert(insertIndex, newGroup);
            Groups = groups.ToArray();
        }

        private void RemoveGroup(AssetGroup group)
        {
            if (group.IsGenerated)
                throw new Exception("Cannot remove generated group.");

            var groups = Groups.ToList();
            groups.Remove(group);
            Groups = groups.ToArray();
        }

        private static AssetGroup[] FilterGroups(AssetGroup[] groups, string searchPattern, bool generated)
        {
            var filtered = groups.Where(x => x.IsGenerated == generated);
            if (string.IsNullOrEmpty(_searchPattern))
                return filtered.ToArray();

            return filtered
                .Select(x =>
                {
                    var match = FuzzySearch.Contains(searchPattern, x.Key.Value, out var score);
                    return (Group: x, Match: match, Score: score);
                })
                .Where(x => x.Match)
                .OrderBy(x => x.Score)
                .Select(x => x.Group)
                .ToArray();
        }

        [Button("Build"), ButtonGroup(order: -1)]
        private void Build()
        {
            AddressableBuilder.Build(this, EditorUserBuildSettings.activeBuildTarget);
        }

        [Button("Generate")]
        private void GenerateGroups()
        {
            var groups = Groups.Where(x => !x.IsGenerated).ToList(); // keep normal groups

            var gen = new List<AssetGroup>();
            var methods = TypeCache.GetMethodsWithAttribute<AssetGroupGeneratorAttribute>();
            foreach (var method in methods)
            {
                var generatorName = method.Name;
                L.I($"[AddressableCatalog] Generating groups from {generatorName}");
                var meta = method.GetCustomAttribute<AssetGroupGeneratorAttribute>();
                var defs = (IEnumerable<AssetGroupGenerationDef>) method.Invoke(null, null);
                gen.AddRange(defs.Select(def => BuildAssetGroup(def, meta, generatorName)));
            }

            // keep original bundle id, if bundle id is not set. (means no direct bundle access)
            L.I("[AddressableCatalog] Assigning original bundle id to generated groups");
            gen.ForEach(x =>
            {
                if (x.BundleId is 0 && TryGetGroup(x.Key, out var orgGroup))
                {
                    L.I($"[AddressableCatalog] Group {x.Key.Value} already exists. Using original bundle id {orgGroup.BundleId.Name()}");
                    x.BundleId = orgGroup.BundleId;
                }
            });

            // sort generated groups (by bundle id, generator id, group key)
            gen.Sort((x, y) =>
            {
                var cmp = x.BundleId.CompareTo(y.BundleId);
                if (cmp != 0) return cmp;
                cmp = string.CompareOrdinal(x.GeneratorName, y.GeneratorName);
                return cmp != 0 ? cmp : string.CompareOrdinal(x.Key.Value, y.Key.Value);
            });

            groups.AddRange(gen);
            Groups = groups.ToArray();
            ClearCache();
            return;

            static AssetGroup BuildAssetGroup(AssetGroupGenerationDef def, AssetGroupGeneratorAttribute meta, string generatorName)
            {
                var groupName = def.GroupName;
                if (groupName is null)
                {
                    Assert.IsTrue(def.Assets.Length is 1, $"Group name is not set, but multiple assets are provided - {generatorName}");
                    groupName = $"{generatorName}_{Path.GetFileNameWithoutExtension(def.Assets[0].Path)}";
                }

                var group = new AssetGroup(groupName, BuildAssetEntries(def)) { GeneratorName = generatorName, };
                Assert.AreEqual(meta.BundleMajor.HasValue, def.BundleMinor.HasValue,
                    $"BundleStart and BundleSubId must be set together - {generatorName}");
                if (!def.BundleMinor.HasValue)
                {
                    L.I($"[AddressableCatalog] Group created: {groupName}");
                    return group;
                }

                group.BundleId = AssetBundleIdUtils.PackBundleId(meta.BundleMajor!.Value, def.BundleMinor.Value);
                L.I($"[AddressableCatalog] Group created: {groupName}, {group.BundleId.Name()}");
                return group;
            }

            static AssetEntry[] BuildAssetEntries(AssetGroupGenerationDef def)
            {
                return def.Assets
                    .Select(x =>
                    {
                        var guid = AssetDatabase.AssetPathToGUID(x.Path);
                        var fileName = Path.GetFileName(x.Path);
                        Assert.IsFalse(string.IsNullOrEmpty(guid),
                            $"Asset not found: address={x.Address}, path={x.Path}");
                        return new AssetEntry(guid, x.Address) { HintName = fileName };
                    })
                    .ToArray();
            }
        }

        [Button("Sort"), ButtonGroup]
        private void SortEntries()
        {
            foreach (var group in Groups.Where(x => !x.IsGenerated))
                group.SortEntries();
            ClearCache();
        }

        internal static bool EditMode;

        [Button("$ToggleEditMode_Label"), ButtonGroup]
        private static void ToggleEditMode() => EditMode = !EditMode;
        private static string ToggleEditMode_Label() => EditMode ? "Edit Done" : "Edit";

        [ContextMenu("Reset Hint Name")]
        private void ResetHintName()
        {
            foreach (var group in Groups)
            foreach (var entry in group.Entries)
                entry.HintName = Path.GetFileName(entry.ResolveAssetPath());
        }
    }
}